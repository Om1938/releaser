using System.Security.Cryptography;
using Releaser.Domain.Common;
using Releaser.Domain.Rollouts;

namespace Releaser.Domain.Deployments;

/// <summary>Makes one release eligible for one audience on one channel, for a share of that audience (ADR 0005).</summary>
public sealed class Deployment : IApplicationScoped
{
    private Deployment() { } // EF Core

    public DeploymentId Id { get; private set; }
    public AppId AppId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public ReleaseId ReleaseId { get; private set; }
    public ChannelKey Channel { get; private set; } = null!;
    public AudienceId AudienceId { get; private set; }
    public RolloutPercentage Percentage { get; private set; }
    public int Priority { get; private set; }
    public string CohortSalt { get; private set; } = string.Empty;
    public DeploymentState State { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Deployment Create(DeploymentPlan plan, DateTimeOffset now) => new()
    {
        Id = DeploymentId.New(),
        AppId = plan.AppId,
        Name = plan.Name,
        ReleaseId = plan.ReleaseId,
        Channel = plan.Channel,
        AudienceId = plan.AudienceId,
        Percentage = plan.Percentage,
        Priority = plan.Priority,
        CohortSalt = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)),
        State = DeploymentState.Draft,
        CreatedAt = now,
        UpdatedAt = now,
    };

    public bool IsTerminal => State is DeploymentState.Completed or DeploymentState.Cancelled;

    public void Activate(DateTimeOffset now) => Transition(DeploymentState.Active, now, DeploymentState.Draft);

    public void Pause(DateTimeOffset now) => Transition(DeploymentState.Paused, now, DeploymentState.Active);

    public void Resume(DateTimeOffset now) => Transition(DeploymentState.Active, now, DeploymentState.Paused);

    public void Cancel(DateTimeOffset now) =>
        Transition(DeploymentState.Cancelled, now, DeploymentState.Draft, DeploymentState.Active, DeploymentState.Paused);

    public void Complete(DateTimeOffset now)
    {
        if (!Percentage.IsFull)
        {
            throw new DomainRuleException("deployment.not_full", "Only a deployment at 100% can be completed.");
        }
        Transition(DeploymentState.Completed, now, DeploymentState.Active);
    }

    public void ChangePercentage(RolloutPercentage percentage, DateTimeOffset now)
    {
        EnsureEditable();
        Percentage = percentage;
        UpdatedAt = now;
    }

    public void ChangePriority(int priority, DateTimeOffset now)
    {
        EnsureEditable();
        Priority = priority;
        UpdatedAt = now;
    }

    private void EnsureEditable()
    {
        if (IsTerminal)
        {
            throw new DomainRuleException("deployment.terminal", $"A {State.ToString().ToUpperInvariant()} deployment can no longer be changed.");
        }
    }

    private void Transition(DeploymentState target, DateTimeOffset now, params DeploymentState[] allowedFrom)
    {
        if (!allowedFrom.Contains(State))
        {
            throw new DomainRuleException("deployment.invalid_transition", $"Cannot move a deployment from {State} to {target}.");
        }
        State = target;
        UpdatedAt = now;
    }
}

/// <summary>Parameters for creating a deployment.</summary>
public sealed record DeploymentPlan(
    AppId AppId,
    string Name,
    ReleaseId ReleaseId,
    ChannelKey Channel,
    AudienceId AudienceId,
    RolloutPercentage Percentage,
    int Priority);
