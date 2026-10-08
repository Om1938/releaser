using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Releaser.Domain.Common;
using Releaser.Domain.ReleaseNotes;
using Releaser.Domain.Releases;
using Releaser.Server.Infrastructure.Http;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Features.ReleaseNotes;

public sealed record PublishedReleaseNote(string Version, string Title, string? Summary, string BodyMarkdown, IReadOnlyList<ReleaseNoteChange> Changes, DateTimeOffset? PublishedAt);

/// <summary>Published release notes for applications to render themselves. Drafts and withdrawn releases are never exposed.</summary>
internal static class ClientReleaseNoteEndpoints
{
    public static RouteGroupBuilder MapClientReleaseNotes(this RouteGroupBuilder client)
    {
        client.MapGet("/apps/{appKey}/notes", ListAsync)
            .WithName("ListPublishedReleaseNotes")
            .WithTags("Client: release notes")
            .WithSummary("Published release notes, newest first")
            .WithDescription("Optional 'version' returns one release; optional 'since' returns releases newer than that version (e.g. the installed one).");
        return client;
    }

    private static async Task<Results<Ok<List<PublishedReleaseNote>>, NotFound, BadRequest>> ListAsync(
        string appKey, string? version, string? since, HttpContext http, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        http.Response.Headers.CacheControl = "public, max-age=60";
        SemanticVersion? exact = null;
        SemanticVersion? lowerBound = null;
        if ((version is not null && !SemanticVersion.TryParse(version, out exact)) || (since is not null && !SemanticVersion.TryParse(since, out lowerBound)))
        {
            return TypedResults.BadRequest();
        }
        ApplicationKey key;
        try
        {
            key = ApplicationKey.From(appKey);
        }
        catch (DomainRuleException)
        {
            return TypedResults.NotFound();
        }
        var appId = await db.Applications.Where(a => a.Key == key).Select(a => (AppId?)a.Id).SingleOrDefaultAsync(cancellationToken);
        if (appId is null)
        {
            return TypedResults.NotFound();
        }
        var rows = await (
            from note in db.ReleaseNotes.AsNoTracking().Where(n => n.AppId == appId.Value && n.State == ReleaseNoteState.Published)
            join release in db.Releases.AsNoTracking().Where(r => r.State != ReleaseState.Withdrawn) on note.ReleaseId equals release.Id
            select new { note, release.Version })
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(rows
            .Where(r => (exact is null || r.Version == exact) && (lowerBound is null || r.Version > lowerBound))
            .OrderByDescending(r => r.Version)
            .Select(r => new PublishedReleaseNote(r.Version.Value, r.note.Content.Title, r.note.Content.Summary, r.note.Content.BodyMarkdown, r.note.Content.Changes, r.note.PublishedAt))
            .ToList());
    }
}
