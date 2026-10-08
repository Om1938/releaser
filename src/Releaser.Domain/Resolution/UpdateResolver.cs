using Releaser.Domain.Common;
using Releaser.Domain.Deployments;
using Releaser.Domain.Releases;
using Releaser.Domain.Rollouts;
using Releaser.Domain.Targeting;

namespace Releaser.Domain.Resolution;

/// <summary>Deterministically decides which release, if any, an installation is offered (ADR 0006).</summary>
public sealed class UpdateResolver
{
    private readonly ResolutionSnapshot _snapshot;
    private readonly TargetContext _context;
    private readonly Dictionary<AudienceId, AudienceSpecificity?> _audienceMatches;
    private readonly List<string> _trace = [];

    private UpdateResolver(ResolutionSnapshot snapshot, TargetContext context)
    {
        _snapshot = snapshot;
        _context = context;
        _audienceMatches = snapshot.Audiences.ToDictionary(audience => audience.Id, audience => audience.Match(context));
    }

    public static UpdateDecision Resolve(ResolutionSnapshot snapshot, TargetContext context) =>
        new UpdateResolver(snapshot, context).Resolve();

    private UpdateDecision Resolve()
    {
        if (!_snapshot.Channels.Contains(_context.Channel))
        {
            return Decide(DecisionReason.UnknownChannel, null, null, null, $"Channel '{_context.Channel}' does not exist.");
        }
        if (!_snapshot.SupportedPlatforms.Contains(_context.Platform))
        {
            return Decide(DecisionReason.PlatformNotSupported, null, null, null, $"The application does not support {_context.Platform}.");
        }
        var pin = SelectPin();
        if (pin is not null)
        {
            _trace.Add($"Pin '{pin.Name}' holds this installation on a specific release; deployments are not considered.");
            return EvaluateTarget(FindRelease(pin.ReleaseId), null, pin);
        }
        var winner = SelectDeployment();
        if (winner is null)
        {
            return Decide(DecisionReason.NoMatchingDeployment, null, null, null, "No deployment on this channel matches the installation and its rollout cohort.");
        }
        var release = FindRelease(winner.ReleaseId);
        _trace.Add($"Deployment '{winner.Name}' ({winner.State}, {winner.Percentage}) selects release {release.Version}.");
        if (winner.State == DeploymentState.Paused)
        {
            return Decide(DecisionReason.DeploymentPaused, release, winner, null, "The deployment is paused: nothing is offered and the installation does not fall through to other deployments.");
        }
        return EvaluateTarget(release, winner, null);
    }

    private PinRule? SelectPin() =>
        _snapshot.Pins
            .Select(pin => (Pin: pin, Specificity: _audienceMatches.GetValueOrDefault(pin.AudienceId)))
            .Where(match => match.Specificity is not null)
            .OrderByDescending(match => match.Specificity)
            .ThenByDescending(match => match.Pin.Priority)
            .ThenBy(match => match.Pin.CreatedAt)
            .ThenBy(match => match.Pin.Id.Value)
            .Select(match => match.Pin)
            .FirstOrDefault();

    private DeploymentRule? SelectDeployment()
    {
        var ranked = _snapshot.Deployments
            .Where(deployment => deployment.Channel == _context.Channel && deployment.ClaimsAudience)
            .Select(deployment => (Deployment: deployment, Specificity: _audienceMatches.GetValueOrDefault(deployment.AudienceId)))
            .Where(IsInAudienceAndCohort)
            .OrderByDescending(match => match.Specificity)
            .ThenByDescending(match => match.Deployment.Priority)
            .ThenByDescending(match => FindRelease(match.Deployment.ReleaseId).Version)
            .ThenBy(match => match.Deployment.Id.Value)
            .Select(match => match.Deployment)
            .ToList();
        if (ranked.Count > 1)
        {
            _trace.Add($"{ranked.Count} deployments match; '{ranked[0].Name}' wins by audience specificity, then priority, then version.");
        }
        return ranked.FirstOrDefault();
    }

    private bool IsInAudienceAndCohort((DeploymentRule Deployment, AudienceSpecificity? Specificity) match)
    {
        if (match.Specificity is null)
        {
            return false;
        }
        var inCohort = CohortSelector.Contains(match.Deployment.CohortSalt, match.Deployment.Percentage, _context.Installation);
        if (!inCohort)
        {
            _trace.Add($"Deployment '{match.Deployment.Name}' matches the audience, but the installation is outside its {match.Deployment.Percentage} cohort.");
        }
        return inCohort;
    }

    private ReleaseCandidate FindRelease(ReleaseId id) => _snapshot.Releases.Single(release => release.Id == id);

    private UpdateDecision EvaluateTarget(ReleaseCandidate release, DeploymentRule? deployment, PinRule? pin)
    {
        if (release.State != ReleaseState.Available || !release.Channels.Contains(_context.Channel))
        {
            return Decide(DecisionReason.ReleaseNotOfferable, release, deployment, pin, $"Release {release.Version} is {release.State} or not on channel '{_context.Channel}'.");
        }
        if (!release.Platforms.Contains(_context.Platform))
        {
            return Decide(DecisionReason.ReleaseNotOnPlatform, release, deployment, pin, $"Release {release.Version} has no {_context.Platform} manifest.");
        }
        var exclusion = _snapshot.Exclusions.FirstOrDefault(rule =>
            rule.ReleaseId == release.Id && _audienceMatches.GetValueOrDefault(rule.AudienceId) is not null);
        if (exclusion is not null)
        {
            return Decide(DecisionReason.ReleaseExcluded, release, deployment, pin, $"Exclusion '{exclusion.Name}' blocks release {release.Version} for this installation.");
        }
        if (release.Version <= _context.CurrentVersion)
        {
            return Decide(DecisionReason.AlreadyUpToDate, release, deployment, pin, $"Current version {_context.CurrentVersion} is not older than {release.Version}; downgrades are never offered.");
        }
        return Decide(DecisionReason.Offered, release, deployment, pin, $"Offering {release.Version}.");
    }

    private UpdateDecision Decide(DecisionReason reason, ReleaseCandidate? release, DeploymentRule? deployment, PinRule? pin, string conclusion)
    {
        _trace.Add(conclusion);
        return new UpdateDecision(reason, release, deployment, pin, _trace);
    }
}
