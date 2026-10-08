using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Releaser.Domain.Common;
using Releaser.Domain.ReleaseNotes;
using Releaser.Domain.Releases;
using Releaser.Server.Features.Audit;
using Releaser.Server.Infrastructure.Http;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Features.Releases;

internal static class ReleaseEndpoints
{
    public static RouteGroupBuilder MapReleases(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/applications/{appId:guid}/releases").WithTags("Releases");
        group.MapGet("/", ListAsync).WithName("ListReleases");
        group.MapGet("/{releaseId:guid}", GetAsync).WithName("GetRelease");
        group.MapPost("/", RegisterAsync).WithName("RegisterRelease").Validate<RegisterReleaseRequest>().RequiresReleaseManager()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group.MapPost("/manifest-preview", PreviewAsync).WithName("PreviewManifest").Validate<PreviewManifestRequest>().RequiresReleaseManager()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group.MapPut("/{releaseId:guid}", UpdateAsync).WithName("UpdateRelease").Validate<UpdateReleaseRequest>().RequiresReleaseManager();
        group.MapPost("/{releaseId:guid}/manifests", AddManifestAsync).WithName("AddReleaseManifest").Validate<ManifestReference>().RequiresReleaseManager()
            .WithSummary("Add a platform's manifest to an existing release (e.g. ship macOS after Windows)")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group.MapPut("/{releaseId:guid}/channels", AssignChannelsAsync).WithName("AssignReleaseChannels").Validate<AssignChannelsRequest>().RequiresReleaseManager();
        group.MapPost("/{releaseId:guid}/deprecate", (Guid appId, Guid releaseId, ReleaserDbContext db, IAuditLog audit, CancellationToken ct) =>
            TransitionAsync(appId, releaseId, db, audit, r => r.Deprecate(), "release.deprecated", ct)).WithName("DeprecateRelease").RequiresReleaseManager();
        group.MapPost("/{releaseId:guid}/withdraw", (Guid appId, Guid releaseId, ReleaserDbContext db, IAuditLog audit, CancellationToken ct) =>
            TransitionAsync(appId, releaseId, db, audit, r => r.Withdraw(), "release.withdrawn", ct)).WithName("WithdrawRelease").RequiresReleaseManager();
        return admin;
    }

    private static async Task<Ok<List<ReleaseResponse>>> ListAsync(Guid appId, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        var id = new AppId(appId);
        var releases = await db.Releases.AsNoTracking().Where(r => r.AppId == id).ToListAsync(cancellationToken);
        var manifests = (await db.ReleaseManifests.AsNoTracking().Where(m => m.AppId == id).ToListAsync(cancellationToken)).ToLookup(m => m.ReleaseId);
        var published = (await db.ReleaseNotes.AsNoTracking().Where(n => n.AppId == id && n.State == ReleaseNoteState.Published)
            .Select(n => n.ReleaseId).ToListAsync(cancellationToken)).ToHashSet();
        return TypedResults.Ok(releases
            .OrderByDescending(r => r.Version)
            .Select(r => ReleaseResponse.From(r, manifests[r.Id], published.Contains(r.Id)))
            .ToList());
    }

    private static async Task<Results<Ok<ReleaseResponse>, NotFound>> GetAsync(Guid appId, Guid releaseId, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        var release = await db.Releases.AsNoTracking().SingleOrDefaultAsync(r => r.AppId == new AppId(appId) && r.Id == new ReleaseId(releaseId), cancellationToken);
        return release is null ? TypedResults.NotFound() : TypedResults.Ok(await ToResponseAsync(release, db, cancellationToken));
    }

    private static async Task<Results<Created<ReleaseResponse>, NotFound>> RegisterAsync(
        Guid appId, RegisterReleaseRequest request, ReleaserDbContext db, ReleaseRegistration registration, IAuditLog audit, CancellationToken cancellationToken)
    {
        var id = new AppId(appId);
        if (!await db.Applications.AnyAsync(a => a.Id == id, cancellationToken))
        {
            return TypedResults.NotFound();
        }
        var (release, manifests) = await registration.RegisterAsync(id, request, cancellationToken);
        audit.Record(new AuditRecord("release.registered", "release", release.Id.ToString(), appId,
            $"version={release.Version}; platforms={string.Join(',', release.Platforms)}; sources={string.Join(' ', manifests.Select(m => m.SourceUrl))}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/admin/v1/applications/{appId}/releases/{release.Id}", ReleaseResponse.From(release, manifests, false));
    }

    private static async Task<Ok<ManifestPreviewResponse>> PreviewAsync(PreviewManifestRequest request, ReleaseRegistration registration, CancellationToken cancellationToken)
    {
        var (manifest, source) = await registration.PreviewAsync(new Uri(request.Url), cancellationToken);
        return TypedResults.Ok(new ManifestPreviewResponse(manifest.Version.Value, source.Sha256,
            [.. manifest.Files.Select(f => new ManifestFileResponse(f.Url, f.Sha512, f.Size))], manifest.ToYaml()));
    }

    private static async Task<Results<Ok<ReleaseResponse>, NotFound>> UpdateAsync(
        Guid appId, Guid releaseId, UpdateReleaseRequest request, ReleaserDbContext db, IAuditLog audit, CancellationToken cancellationToken)
    {
        var release = await FindAsync(appId, releaseId, db, cancellationToken);
        if (release is null)
        {
            return TypedResults.NotFound();
        }
        release.Rename(request.Title);
        audit.Record(new AuditRecord("release.updated", "release", release.Id.ToString(), appId, $"title={request.Title}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await ToResponseAsync(release, db, cancellationToken));
    }

    private static async Task<Results<Ok<ReleaseResponse>, NotFound>> AddManifestAsync(
        Guid appId, Guid releaseId, ManifestReference request, ReleaserDbContext db, ReleaseRegistration registration, IAuditLog audit, CancellationToken cancellationToken)
    {
        var detached = await db.Releases.AsNoTracking().SingleOrDefaultAsync(r => r.AppId == new AppId(appId) && r.Id == new ReleaseId(releaseId), cancellationToken);
        if (detached is null)
        {
            return TypedResults.NotFound();
        }
        // Fetch first (up to the manifest timeout), then append in a short optimistic transaction. Concurrent adds of
        // different platforms only conflict on the release row version, so retry on fresh state (issue #8 review).
        var prepared = await registration.PrepareAdditionAsync(detached, request, cancellationToken);
        for (var attempt = 1; ; attempt++)
        {
            var release = (await FindAsync(appId, releaseId, db, cancellationToken))!;
            var manifest = registration.Attach(release, prepared);
            audit.Record(new AuditRecord("release.manifest_added", "release", release.Id.ToString(), appId,
                $"version={release.Version}; platform={manifest.Platform}; source={manifest.SourceUrl}"));
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return TypedResults.Ok(await ToResponseAsync(release, db, cancellationToken));
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAppendAttempts)
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    private const int MaxAppendAttempts = 3;

    private static async Task<Results<Ok<ReleaseResponse>, NotFound>> AssignChannelsAsync(
        Guid appId, Guid releaseId, AssignChannelsRequest request, ReleaserDbContext db, IAuditLog audit, CancellationToken cancellationToken)
    {
        var release = await FindAsync(appId, releaseId, db, cancellationToken);
        if (release is null)
        {
            return TypedResults.NotFound();
        }
        var requested = request.Channels.Distinct().Select(ChannelKey.From).ToList();
        var known = await db.Channels.Where(c => c.AppId == release.AppId).Select(c => c.Key).ToListAsync(cancellationToken);
        var unknown = requested.Except(known).ToList();
        if (unknown.Count > 0)
        {
            throw new DomainRuleException("release.unknown_channel", $"Unknown channel(s): {string.Join(", ", unknown)}.");
        }
        foreach (var channel in release.Channels.Except(requested).ToList())
        {
            release.UnassignChannel(channel);
        }
        requested.ForEach(release.AssignChannel);
        audit.Record(new AuditRecord("release.channels_assigned", "release", release.Id.ToString(), appId, $"channels={string.Join(',', requested)}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await ToResponseAsync(release, db, cancellationToken));
    }

    private static async Task<Results<Ok<ReleaseResponse>, NotFound>> TransitionAsync(
        Guid appId, Guid releaseId, ReleaserDbContext db, IAuditLog audit, Action<Release> transition, string action, CancellationToken cancellationToken)
    {
        var release = await FindAsync(appId, releaseId, db, cancellationToken);
        if (release is null)
        {
            return TypedResults.NotFound();
        }
        transition(release);
        audit.Record(new AuditRecord(action, "release", release.Id.ToString(), appId, $"version={release.Version}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await ToResponseAsync(release, db, cancellationToken));
    }

    private static Task<Release?> FindAsync(Guid appId, Guid releaseId, ReleaserDbContext db, CancellationToken cancellationToken) =>
        db.Releases.SingleOrDefaultAsync(r => r.AppId == new AppId(appId) && r.Id == new ReleaseId(releaseId), cancellationToken);

    private static async Task<ReleaseResponse> ToResponseAsync(Release release, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        var manifests = await db.ReleaseManifests.AsNoTracking().Where(m => m.ReleaseId == release.Id).ToListAsync(cancellationToken);
        var published = await db.ReleaseNotes.AnyAsync(n => n.ReleaseId == release.Id && n.State == ReleaseNoteState.Published, cancellationToken);
        return ReleaseResponse.From(release, manifests, published);
    }
}
