using Releaser.Domain.Common;

namespace Releaser.Domain.Resolution;

public enum DecisionReason
{
    /// <summary>A newer release is offered by a pin or the winning deployment.</summary>
    Offered = 1,
    UnknownChannel,
    NoMatchingDeployment,
    DeploymentPaused,
    ReleaseNotOfferable,
    ReleaseNotOnPlatform,
    ReleaseExcluded,
    AlreadyUpToDate,
    PlatformNotSupported,
}

/// <summary>The resolved answer plus a human-readable trace explaining it (ADR 0006). The trace is for administrators only.</summary>
public sealed record UpdateDecision(
    DecisionReason Reason,
    ReleaseCandidate? Release,
    DeploymentRule? Deployment,
    PinRule? AppliedPin,
    IReadOnlyList<string> Trace)
{
    public bool IsUpdateOffered => Reason == DecisionReason.Offered;

    public SemanticVersion? OfferedVersion => IsUpdateOffered ? Release!.Version : null;
}
