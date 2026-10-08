using Releaser.Domain.Common;

namespace Releaser.Domain.Policies;

/// <summary>Release pin or release exclusion applied to an audience across all channels of an application (ADR 0006).</summary>
public sealed class Policy : IApplicationScoped
{
    private Policy() { } // EF Core

    public PolicyId Id { get; private set; }
    public AppId AppId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public PolicyKind Kind { get; private set; }
    public AudienceId AudienceId { get; private set; }
    public ReleaseId ReleaseId { get; private set; }
    public int Priority { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Policy Pin(AppId applicationId, string name, AudienceId audienceId, ReleaseId releaseId, int priority, DateTimeOffset now) => new()
    {
        Id = PolicyId.New(),
        AppId = applicationId,
        Name = name,
        Kind = PolicyKind.Pin,
        AudienceId = audienceId,
        ReleaseId = releaseId,
        Priority = priority,
        CreatedAt = now,
    };

    public static Policy Exclude(AppId applicationId, string name, AudienceId audienceId, ReleaseId releaseId, DateTimeOffset now) => new()
    {
        Id = PolicyId.New(),
        AppId = applicationId,
        Name = name,
        Kind = PolicyKind.Exclusion,
        AudienceId = audienceId,
        ReleaseId = releaseId,
        CreatedAt = now,
    };
}
