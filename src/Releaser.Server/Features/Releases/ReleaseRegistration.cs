using Microsoft.EntityFrameworkCore;
using Releaser.Domain.Common;
using Releaser.Domain.Releases;
using Releaser.Electron;
using Releaser.Server.Infrastructure.Manifests;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Features.Releases;

/// <summary>A validated manifest snapshot that has been fetched but not yet attached to a release.</summary>
internal sealed record PreparedManifest(ManifestReference Reference, FetchedManifest Source, ElectronManifest Manifest);

/// <summary>Fetches, validates and snapshots external manifests, then registers the release (ADR 0002, ADR 0003, ADR 0005).</summary>
internal sealed class ReleaseRegistration(ReleaserDbContext db, IManifestFetcher fetcher, TimeProvider clock)
{
    public async Task<(Release Release, List<ReleaseManifest> Manifests)> RegisterAsync(AppId appId, RegisterReleaseRequest request, CancellationToken cancellationToken)
    {
        var version = SemanticVersion.Parse(request.Version);
        var channels = request.Channels.Distinct().Select(ChannelKey.From).ToList();
        await EnsureChannelsExistAsync(appId, channels, cancellationToken);
        await EnsureVersionIsNewAsync(appId, version, cancellationToken);
        var application = await db.Applications.AsNoTracking().SingleAsync(a => a.Id == appId, cancellationToken);
        foreach (var reference in request.Manifests)
        {
            application.EnsureSupports(reference.Platform);
        }

        var prepared = await Task.WhenAll(request.Manifests.Select(reference => PrepareForRegistrationAsync(reference, version, cancellationToken)));
        var now = clock.GetUtcNow();
        var release = Release.Register(appId, version, request.Title, prepared.Select(p => p.Reference.Platform), now);
        channels.ForEach(release.AssignChannel);
        var manifests = prepared.Select(p => ToSnapshot(release, p, now)).ToList();
        db.Releases.Add(release);
        db.ReleaseManifests.AddRange(manifests);
        return (release, manifests);
    }

    public async Task<(ElectronManifest Manifest, FetchedManifest Source)> PreviewAsync(Uri url, CancellationToken cancellationToken)
    {
        var source = await fetcher.FetchAsync(url, cancellationToken);
        return (ElectronManifest.Parse(source.Content).WithAbsoluteUrls(source.Url), source);
    }

    /// <summary>
    /// Fetches and validates a manifest for <paramref name="release"/> without holding it tracked, so the remote
    /// fetch never overlaps the optimistic-concurrency window of the save. Fails fast on rules that need no fetch.
    /// </summary>
    public async Task<PreparedManifest> PrepareAdditionAsync(Release release, ManifestReference reference, CancellationToken cancellationToken)
    {
        release.AddPlatform(reference.Platform); // detached copy: validates "withdrawn" and "already has this platform" up front
        var application = await db.Applications.AsNoTracking().SingleAsync(a => a.Id == release.AppId, cancellationToken);
        application.EnsureSupports(reference.Platform);
        return await PrepareAsync(reference, release.Version, cancellationToken);
    }

    /// <summary>Appends a prepared manifest to the tracked release (append-only; ADR 0005).</summary>
    public ReleaseManifest Attach(Release release, PreparedManifest prepared)
    {
        release.AddPlatform(prepared.Reference.Platform);
        var snapshot = ToSnapshot(release, prepared, clock.GetUtcNow());
        db.ReleaseManifests.Add(snapshot);
        return snapshot;
    }

    private static ReleaseManifest ToSnapshot(Release release, PreparedManifest prepared, DateTimeOffset now) =>
        ReleaseManifest.Snapshot(release.AppId, release.Id, prepared.Reference.Platform, prepared.Source.Url, prepared.Source.Sha256, prepared.Manifest.ToYaml(), now);

    private async Task<PreparedManifest> PrepareForRegistrationAsync(ManifestReference reference, SemanticVersion version, CancellationToken cancellationToken)
    {
        try
        {
            return await PrepareAsync(reference, version, cancellationToken);
        }
        catch (ManifestFetchException exception) when (exception.IsMissingFile)
        {
            throw new ManifestFetchException(
                $"{exception.Message} If this platform isn't published yet, untick it to register the others now and add it to the release later.",
                exception.StatusCode, exception);
        }
    }

    /// <summary>Fetches, parses and version-checks one manifest. Every failure names the platform.</summary>
    private async Task<PreparedManifest> PrepareAsync(ManifestReference reference, SemanticVersion version, CancellationToken cancellationToken)
    {
        try
        {
            var (manifest, source) = await PreviewAsync(new Uri(reference.Url), cancellationToken);
            if (manifest.Version != version)
            {
                throw new DomainRuleException("manifest.version_mismatch", $"declares version {manifest.Version}, not {version}.");
            }
            return new PreparedManifest(reference, source, manifest);
        }
        catch (ManifestFetchException exception)
        {
            throw new ManifestFetchException($"{reference.Platform} manifest: {exception.Message}", exception.StatusCode, exception);
        }
        catch (DomainRuleException exception) when (exception.Code.StartsWith("manifest.", StringComparison.Ordinal))
        {
            throw new DomainRuleException(exception.Code, $"{reference.Platform} manifest: {exception.Message}");
        }
    }

    private async Task EnsureChannelsExistAsync(AppId appId, List<ChannelKey> channels, CancellationToken cancellationToken)
    {
        var existing = await db.Channels.Where(c => c.AppId == appId).Select(c => c.Key).ToListAsync(cancellationToken);
        var missing = channels.Except(existing).ToList();
        if (missing.Count > 0)
        {
            throw new DomainRuleException("release.unknown_channel", $"Unknown channel(s): {string.Join(", ", missing)}.");
        }
    }

    private async Task EnsureVersionIsNewAsync(AppId appId, SemanticVersion version, CancellationToken cancellationToken)
    {
        if (await db.Releases.AnyAsync(r => r.AppId == appId && r.Version == version, cancellationToken))
        {
            throw new DomainRuleException("release.duplicate_version",
                $"Version {version} is already registered. To register it again, an admin can obliterate the existing release from its page (this cannot be undone).");
        }
    }
}
