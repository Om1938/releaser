namespace Releaser.Server.Features.Audit;

/// <summary>Immutable record of a consequential administrative change.</summary>
public sealed class AuditEntry
{
    private AuditEntry() { } // EF Core

    public long Id { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid? ActorId { get; private set; }
    public string Actor { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public Guid? AppId { get; private set; }
    public string? Details { get; private set; }

    public static AuditEntry Create(AuditActor actor, AuditRecord record, DateTimeOffset now) => new()
    {
        OccurredAt = now,
        ActorId = actor.Id,
        Actor = actor.Name,
        Action = record.Action,
        EntityType = record.EntityType,
        EntityId = record.EntityId,
        AppId = record.AppId,
        Details = record.Details,
    };
}

public sealed record AuditActor(Guid? Id, string Name)
{
    public static AuditActor System { get; } = new(null, "system");
}

/// <summary>What happened to which entity. Details is a short JSON or text description without secrets.</summary>
public sealed record AuditRecord(string Action, string EntityType, string EntityId, Guid? AppId = null, string? Details = null);
