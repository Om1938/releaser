using System.Security.Claims;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Features.Audit;

/// <summary>Adds audit entries to the current unit of work so they commit atomically with the change they describe.</summary>
public interface IAuditLog
{
    void Record(AuditRecord record);
}

internal sealed class AuditLog(ReleaserDbContext db, IHttpContextAccessor httpContextAccessor, TimeProvider clock) : IAuditLog
{
    public void Record(AuditRecord record) =>
        db.AuditEntries.Add(AuditEntry.Create(CurrentActor(), record, clock.GetUtcNow()));

    private AuditActor CurrentActor()
    {
        var user = httpContextAccessor.HttpContext?.User;
        var id = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        return id is null || user?.Identity?.Name is not { } name
            ? AuditActor.System
            : new AuditActor(Guid.Parse(id), name);
    }
}
