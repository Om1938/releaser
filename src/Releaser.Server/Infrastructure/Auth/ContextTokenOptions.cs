using System.ComponentModel.DataAnnotations;

namespace Releaser.Server.Infrastructure.Auth;

public sealed class ContextTokenOptions
{
    public const string Section = "ContextTokens";

    /// <summary>Maximum accepted token lifetime (exp - iat). Long-lived tokens are rejected because updaters also send headers to artifact hosts.</summary>
    [Range(1, 168)]
    public int MaxLifetimeHours { get; set; } = 24;

    [Range(0, 600)]
    public int ClockSkewSeconds { get; set; } = 60;
}
