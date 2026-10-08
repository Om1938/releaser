using Releaser.Domain.Common;
using Releaser.Domain.Deployments;
using Releaser.Domain.Rollouts;

namespace Releaser.Domain.Tests;

public sealed class DeploymentLifecycleTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static Deployment NewDeployment(decimal percent = 20m) =>
        Deployment.Create(new DeploymentPlan(AppId.New(), "Customer B", ReleaseId.New(), ChannelKey.From("stable"),
            AudienceId.New(), RolloutPercentage.From(percent), 0), Now);

    [Fact]
    public void a_new_deployment_starts_as_draft_with_a_random_salt()
    {
        var first = NewDeployment();
        first.State.ShouldBe(DeploymentState.Draft);
        first.CohortSalt.ShouldNotBe(NewDeployment().CohortSalt);
    }

    [Fact]
    public void draft_can_be_activated_paused_resumed_and_cancelled()
    {
        var deployment = NewDeployment();
        deployment.Activate(Now);
        deployment.Pause(Now);
        deployment.State.ShouldBe(DeploymentState.Paused);
        deployment.Resume(Now);
        deployment.State.ShouldBe(DeploymentState.Active);
        deployment.Cancel(Now);
        deployment.State.ShouldBe(DeploymentState.Cancelled);
    }

    [Fact]
    public void completing_below_100_percent_is_rejected()
    {
        var deployment = NewDeployment(20m);
        deployment.Activate(Now);
        Should.Throw<DomainRuleException>(() => deployment.Complete(Now)).Code.ShouldBe("deployment.not_full");
    }

    [Fact]
    public void completed_deployment_is_locked()
    {
        var deployment = NewDeployment(100m);
        deployment.Activate(Now);
        deployment.Complete(Now);
        Should.Throw<DomainRuleException>(() => deployment.ChangePercentage(RolloutPercentage.From(50m), Now));
        Should.Throw<DomainRuleException>(() => deployment.Pause(Now));
    }

    [Fact]
    public void percentage_can_change_while_active_without_changing_the_salt()
    {
        var deployment = NewDeployment(20m);
        deployment.Activate(Now);
        var salt = deployment.CohortSalt;
        deployment.ChangePercentage(RolloutPercentage.From(50m), Now);
        deployment.Percentage.ShouldBe(RolloutPercentage.From(50m));
        deployment.CohortSalt.ShouldBe(salt);
    }

    [Theory]
    [InlineData("pause")]
    [InlineData("resume")]
    [InlineData("complete")]
    public void invalid_transitions_from_draft_are_rejected(string action)
    {
        var deployment = NewDeployment(100m);
        Action act = action switch
        {
            "pause" => () => deployment.Pause(Now),
            "resume" => () => deployment.Resume(Now),
            _ => () => deployment.Complete(Now),
        };
        Should.Throw<DomainRuleException>(act).Code.ShouldBe("deployment.invalid_transition");
    }
}
