using Releaser.Domain.Common;
using Releaser.Domain.Deployments;
using Releaser.Domain.Releases;
using Releaser.Domain.Resolution;
using Releaser.Domain.Rollouts;
using Releaser.Domain.Targeting;

namespace Releaser.Domain.Tests;

/// <summary>Builds resolution snapshots in test-readable steps.</summary>
internal sealed class ScenarioBuilder
{
    public static readonly ChannelKey Stable = ChannelKey.From("stable");
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly List<ReleaseCandidate> _releases = [];
    private readonly List<DeploymentRule> _deployments = [];
    private readonly List<AudienceDefinition> _audiences = [];
    private readonly List<PinRule> _pins = [];
    private readonly List<ExclusionRule> _exclusions = [];

    public ReleaseCandidate Release(string version, ReleaseState state = ReleaseState.Available, params PlatformTarget[] platforms)
    {
        var release = new ReleaseCandidate(ReleaseId.New(), SemanticVersion.Parse(version), state, [Stable],
            platforms.Length == 0 ? [PlatformTarget.Windows, PlatformTarget.MacOS, PlatformTarget.LinuxX64] : platforms);
        _releases.Add(release);
        return release;
    }

    public AudienceDefinition Audience(params AudienceRule[] includes) => AudienceExcluding(includes, []);

    public AudienceDefinition AudienceExcluding(AudienceRule[] includes, AudienceRule[] excludes)
    {
        var audience = new AudienceDefinition(AudienceId.New(), includes, excludes);
        _audiences.Add(audience);
        return audience;
    }

    public DeploymentRule Deploy(string name, ReleaseCandidate release, AudienceDefinition audience, decimal percent = 100m,
        DeploymentState state = DeploymentState.Active, int priority = 0)
    {
        var deployment = new DeploymentRule(DeploymentId.New(), name, release.Id, Stable, audience.Id,
            RolloutPercentage.From(percent), $"salt-{name}", state, priority);
        _deployments.Add(deployment);
        return deployment;
    }

    public void Pin(string name, AudienceDefinition audience, ReleaseCandidate release, int priority = 0) =>
        _pins.Add(new PinRule(PolicyId.New(), name, audience.Id, release.Id, priority, Now));

    public void Exclude(string name, AudienceDefinition audience, ReleaseCandidate release) =>
        _exclusions.Add(new ExclusionRule(PolicyId.New(), name, audience.Id, release.Id));

    public void Replace(DeploymentRule deployment, DeploymentRule replacement) =>
        _deployments[_deployments.IndexOf(deployment)] = replacement;

    public ResolutionSnapshot Build() =>
        new(1, Stable, [Stable], [.. _releases], [.. _deployments], [.. _audiences], [.. _pins], [.. _exclusions]);

    public static TargetContext Context(string current = "1.0.0", string? installation = "install-0001",
        string? customer = null, string? user = null, string[]? groups = null, PlatformTarget platform = PlatformTarget.Windows) =>
        new(Stable, platform, SemanticVersion.Parse(current),
            installation is null ? null : InstallationId.From(installation),
            new VerifiedIdentity(user, customer, new HashSet<string>(groups ?? []), new Dictionary<string, string>()));
}
