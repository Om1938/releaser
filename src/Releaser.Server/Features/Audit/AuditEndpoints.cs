using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Features.Audit;

public sealed record AuditEntryResponse(long Id, DateTimeOffset OccurredAt, string Actor, string Action, string EntityType, string EntityId, Guid? AppId, string? Details);

public sealed record AuditPage(IReadOnlyList<AuditEntryResponse> Entries, long? NextBefore);

internal static class AuditEndpoints
{
    private const int MaxPageSize = 200;

    public static RouteGroupBuilder MapAudit(this RouteGroupBuilder admin)
    {
        admin.MapGet("/audit", ListAsync).WithName("ListAuditEntries").WithTags("Audit")
            .WithSummary("Audit trail, newest first; page with 'before' (the previous page's nextBefore).");
        return admin;
    }

    private static async Task<Ok<AuditPage>> ListAsync(Guid? appId, long? before, int? limit, string? action, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        var size = Math.Clamp(limit ?? 50, 1, MaxPageSize);
        var query = db.AuditEntries.AsNoTracking();
        if (appId is not null)
        {
            query = query.Where(e => e.AppId == appId);
        }
        if (before is not null)
        {
            query = query.Where(e => e.Id < before);
        }
        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(e => e.Action.StartsWith(action));
        }
        var entries = await query.OrderByDescending(e => e.Id).Take(size + 1)
            .Select(e => new AuditEntryResponse(e.Id, e.OccurredAt, e.Actor, e.Action, e.EntityType, e.EntityId, e.AppId, e.Details))
            .ToListAsync(cancellationToken);
        var hasMore = entries.Count > size;
        var page = entries.Take(size).ToList();
        return TypedResults.Ok(new AuditPage(page, hasMore ? page[^1].Id : null));
    }
}
