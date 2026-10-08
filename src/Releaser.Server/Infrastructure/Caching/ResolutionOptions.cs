using System.ComponentModel.DataAnnotations;

namespace Releaser.Server.Infrastructure.Caching;

public sealed class ResolutionOptions
{
    public const string Section = "Resolution";

    /// <summary>Upper bound, in seconds, for how long any node may keep resolving with a superseded configuration (ADR 0008).</summary>
    [Range(1, 300)]
    public int FreshnessSeconds { get; set; } = 5;

    /// <summary>How long immutable snapshots stay in the local (L1) cache.</summary>
    [Range(1, 1440)]
    public int SnapshotLocalMinutes { get; set; } = 10;
}
