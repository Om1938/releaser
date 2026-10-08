using Releaser.Domain.Common;
using Releaser.Domain.Deployments;
using Releaser.Domain.Releases;
using Releaser.Domain.Rollouts;
using Releaser.Domain.Targeting;

namespace Releaser.Domain.Resolution;

/// <summary>Immutable, cacheable input for resolving updates of one application at one configuration version.</summary>
public sealed record ResolutionSnapshot(
    long ConfigVersion,
    ChannelKey DefaultChannel,
    IReadOnlyList<ChannelKey> Channels,
    IReadOnlyList<PlatformTarget> SupportedPlatforms,
    IReadOnlyList<ReleaseCandidate> Releases,
    IReadOnlyList<DeploymentRule> Deployments,
    IReadOnlyList<AudienceDefinition> Audiences,
    IReadOnlyList<PinRule> Pins,
    IReadOnlyList<ExclusionRule> Exclusions);

public sealed record ReleaseCandidate(
    ReleaseId Id,
    SemanticVersion Version,
    ReleaseState State,
    IReadOnlyList<ChannelKey> Channels,
    IReadOnlyList<PlatformTarget> Platforms)
{
    public static ReleaseCandidate From(Release release) =>
        new(release.Id, release.Version, release.State, [.. release.Channels], [.. release.Platforms]);
}

public sealed record DeploymentRule(
    DeploymentId Id,
    string Name,
    ReleaseId ReleaseId,
    ChannelKey Channel,
    AudienceId AudienceId,
    RolloutPercentage Percentage,
    string CohortSalt,
    DeploymentState State,
    int Priority)
{
    public static DeploymentRule From(Deployment deployment) =>
        new(deployment.Id, deployment.Name, deployment.ReleaseId, deployment.Channel, deployment.AudienceId,
            deployment.Percentage, deployment.CohortSalt, deployment.State, deployment.Priority);

    /// <summary>Whether the deployment claims its audience (offering or holding). Draft and cancelled deployments do not.</summary>
    public bool ClaimsAudience => State is DeploymentState.Active or DeploymentState.Paused or DeploymentState.Completed;
}

public sealed record PinRule(PolicyId Id, string Name, AudienceId AudienceId, ReleaseId ReleaseId, int Priority, DateTimeOffset CreatedAt);

public sealed record ExclusionRule(PolicyId Id, string Name, AudienceId AudienceId, ReleaseId ReleaseId);
