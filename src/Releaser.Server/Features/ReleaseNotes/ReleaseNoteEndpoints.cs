using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Releaser.Domain.Common;
using Releaser.Domain.ReleaseNotes;
using Releaser.Server.Features.Audit;
using Releaser.Server.Infrastructure.Http;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Features.ReleaseNotes;

public sealed record ReleaseNoteResponse(
    Guid Id, Guid ReleaseId, ReleaseNoteState State, int Revision, string Title, string? Summary, string BodyMarkdown,
    IReadOnlyList<ReleaseNoteChange> Changes, DateTimeOffset UpdatedAt, DateTimeOffset? PublishedAt)
{
    public static ReleaseNoteResponse From(ReleaseNote n) =>
        new(n.Id, n.ReleaseId.Value, n.State, n.Revision, n.Content.Title, n.Content.Summary, n.Content.BodyMarkdown, n.Content.Changes, n.UpdatedAt, n.PublishedAt);
}

public sealed record ReleaseNoteRevisionResponse(int Revision, ReleaseNoteState State, string Title, string? Summary, string BodyMarkdown,
    IReadOnlyList<ReleaseNoteChange> Changes, string? Actor, DateTimeOffset RecordedAt);

public sealed record SaveReleaseNoteRequest(string Title, string? Summary, string BodyMarkdown, IReadOnlyList<ReleaseNoteChange> Changes);

internal sealed class SaveReleaseNoteValidator : AbstractValidator<SaveReleaseNoteRequest>
{
    public SaveReleaseNoteValidator()
    {
        RuleFor(r => r.Title).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Summary).MaximumLength(1000);
        RuleFor(r => r.BodyMarkdown).NotNull().MaximumLength(50_000);
        RuleFor(r => r.Changes).NotNull().Must(c => c.Count <= 200);
        RuleForEach(r => r.Changes).ChildRules(change =>
        {
            change.RuleFor(c => c.Category).IsInEnum();
            change.RuleFor(c => c.Text).NotEmpty().MaximumLength(1000);
        });
    }
}

internal static class ReleaseNoteEndpoints
{
    public static RouteGroupBuilder MapReleaseNotes(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/applications/{appId:guid}/releases/{releaseId:guid}/notes").WithTags("Release notes");
        group.MapGet("/", GetAsync).WithName("GetReleaseNote");
        group.MapPut("/", SaveAsync).WithName("SaveReleaseNote").Validate<SaveReleaseNoteRequest>().RequiresReleaseManager();
        group.MapPost("/publish", (Guid appId, Guid releaseId, ReleaserDbContext db, IAuditLog audit, TimeProvider clock, HttpContext http, CancellationToken ct) =>
            ChangeStateAsync(appId, releaseId, db, audit, http, (n, now) => n.Publish(now), "release_note.published", clock, ct)).WithName("PublishReleaseNote").RequiresReleaseManager();
        group.MapPost("/unpublish", (Guid appId, Guid releaseId, ReleaserDbContext db, IAuditLog audit, TimeProvider clock, HttpContext http, CancellationToken ct) =>
            ChangeStateAsync(appId, releaseId, db, audit, http, (n, now) => n.Unpublish(now), "release_note.unpublished", clock, ct)).WithName("UnpublishReleaseNote").RequiresReleaseManager();
        group.MapGet("/revisions", ListRevisionsAsync).WithName("ListReleaseNoteRevisions");
        return admin;
    }

    private static async Task<Results<Ok<ReleaseNoteResponse>, NotFound>> GetAsync(Guid appId, Guid releaseId, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        var note = await FindAsync(appId, releaseId, db, cancellationToken);
        return note is null ? TypedResults.NotFound() : TypedResults.Ok(ReleaseNoteResponse.From(note));
    }

    private static async Task<Results<Ok<ReleaseNoteResponse>, NotFound>> SaveAsync(
        Guid appId, Guid releaseId, SaveReleaseNoteRequest request, ReleaserDbContext db, IAuditLog audit, TimeProvider clock, HttpContext http, CancellationToken cancellationToken)
    {
        var id = new AppId(appId);
        if (!await db.Releases.AnyAsync(r => r.AppId == id && r.Id == new ReleaseId(releaseId), cancellationToken))
        {
            return TypedResults.NotFound();
        }
        var content = new ReleaseNoteContent(request.Title, request.Summary, request.BodyMarkdown, [.. request.Changes]);
        var note = await FindAsync(appId, releaseId, db, cancellationToken);
        if (note is null)
        {
            note = ReleaseNote.Draft(id, new ReleaseId(releaseId), content, clock.GetUtcNow());
            db.ReleaseNotes.Add(note);
        }
        else
        {
            note.Edit(content, clock.GetUtcNow());
        }
        db.ReleaseNoteRevisions.Add(ReleaseNoteRevision.Of(note, http.User.Identity?.Name));
        audit.Record(new AuditRecord("release_note.saved", "release_note", note.Id.ToString(), appId, $"release={releaseId}; revision={note.Revision}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ReleaseNoteResponse.From(note));
    }

    private static async Task<Results<Ok<ReleaseNoteResponse>, NotFound>> ChangeStateAsync(
        Guid appId, Guid releaseId, ReleaserDbContext db, IAuditLog audit, HttpContext http,
        Action<ReleaseNote, DateTimeOffset> change, string action, TimeProvider clock, CancellationToken cancellationToken)
    {
        var note = await FindAsync(appId, releaseId, db, cancellationToken);
        if (note is null)
        {
            return TypedResults.NotFound();
        }
        change(note, clock.GetUtcNow());
        db.ReleaseNoteRevisions.Add(ReleaseNoteRevision.Of(note, http.User.Identity?.Name));
        audit.Record(new AuditRecord(action, "release_note", note.Id.ToString(), appId, $"release={releaseId}; revision={note.Revision}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ReleaseNoteResponse.From(note));
    }

    private static async Task<Ok<List<ReleaseNoteRevisionResponse>>> ListRevisionsAsync(Guid appId, Guid releaseId, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        var note = await FindAsync(appId, releaseId, db, cancellationToken);
        if (note is null)
        {
            return TypedResults.Ok(new List<ReleaseNoteRevisionResponse>());
        }
        var revisions = await db.ReleaseNoteRevisions.AsNoTracking().Where(r => r.ReleaseNoteId == note.Id).OrderByDescending(r => r.Revision).ToListAsync(cancellationToken);
        return TypedResults.Ok(revisions.Select(r => new ReleaseNoteRevisionResponse(r.Revision, r.State, r.Content.Title, r.Content.Summary,
            r.Content.BodyMarkdown, r.Content.Changes, r.Actor, r.RecordedAt)).ToList());
    }

    private static Task<ReleaseNote?> FindAsync(Guid appId, Guid releaseId, ReleaserDbContext db, CancellationToken cancellationToken) =>
        db.ReleaseNotes.SingleOrDefaultAsync(n => n.AppId == new AppId(appId) && n.ReleaseId == new ReleaseId(releaseId), cancellationToken);
}
