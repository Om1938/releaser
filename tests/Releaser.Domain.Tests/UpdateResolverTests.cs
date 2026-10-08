using System.Text.Json;
using Releaser.Domain.Deployments;
using Releaser.Domain.Releases;
using Releaser.Domain.Resolution;
using Releaser.Domain.Targeting;
using static Releaser.Domain.Tests.ScenarioBuilder;

namespace Releaser.Domain.Tests;

public sealed class UpdateResolverTests
{
    /// <summary>PRD §6: A→1.1.0 100%, B→1.2.0 20%, QA→1.2.0 100%, everyone→1.0.0.</summary>
    private sealed class PrdScenario
    {
        public readonly ScenarioBuilder Builder = new();
        public readonly ReleaseCandidate V100, V110, V120;
        public readonly DeploymentRule CustomerBDeployment;

        public PrdScenario()
        {
            V100 = Builder.Release("1.0.0");
            V110 = Builder.Release("1.1.0");
            V120 = Builder.Release("1.2.0");
            Builder.Deploy("Default", V100, Builder.Audience(new EveryoneRule()));
            Builder.Deploy("Customer A", V110, Builder.Audience(new CustomerRule(["customer-a"])));
            CustomerBDeployment = Builder.Deploy("Customer B", V120, Builder.Audience(new CustomerRule(["customer-b"])), percent: 20m);
            Builder.Deploy("Internal QA", V120, Builder.Audience(new GroupRule(["internal-qa"])));
        }

        public UpdateDecision Resolve(TargetContext context) => UpdateResolver.Resolve(Builder.Build(), context);
    }

    private static IEnumerable<string> InstallationIds(int count) => Enumerable.Range(0, count).Select(i => $"install-{i:D5}");

    [Fact]
    public void customer_a_on_1_0_0_is_offered_1_1_0()
    {
        var decision = new PrdScenario().Resolve(Context(current: "1.0.0", customer: "customer-a"));
        decision.Reason.ShouldBe(DecisionReason.Offered);
        decision.OfferedVersion!.ToString().ShouldBe("1.1.0");
    }

    [Fact]
    public void internal_qa_is_offered_1_2_0()
    {
        new PrdScenario().Resolve(Context(groups: ["internal-qa"])).OfferedVersion!.ToString().ShouldBe("1.2.0");
    }

    [Fact]
    public void when_a_customer_a_user_is_also_in_qa_the_more_specific_group_deployment_wins()
    {
        var decision = new PrdScenario().Resolve(Context(customer: "customer-a", groups: ["internal-qa"]));
        decision.Deployment!.Name.ShouldBe("Internal QA");
        decision.OfferedVersion!.ToString().ShouldBe("1.2.0");
    }

    [Fact]
    public void an_anonymous_installation_on_1_0_0_gets_no_update_from_the_default_deployment()
    {
        var decision = new PrdScenario().Resolve(Context(current: "1.0.0"));
        decision.Reason.ShouldBe(DecisionReason.AlreadyUpToDate);
        decision.Deployment!.Name.ShouldBe("Default");
    }

    [Fact]
    public void about_20_percent_of_customer_b_is_offered_1_2_0_and_the_rest_falls_back_to_default()
    {
        var scenario = new PrdScenario();
        var decisions = InstallationIds(2_000)
            .Select(id => scenario.Resolve(Context(customer: "customer-b", installation: id)))
            .ToList();

        decisions.Count(d => d.OfferedVersion?.ToString() == "1.2.0").ShouldBeInRange(330, 470);
        decisions.Where(d => !d.IsUpdateOffered).ShouldAllBe(d => d.Deployment!.Name == "Default");
    }

    [Fact]
    public void customer_b_cohort_is_identical_across_repeated_checks()
    {
        var scenario = new PrdScenario();
        List<bool> Check() => [.. InstallationIds(1_000).Select(id => scenario.Resolve(Context(customer: "customer-b", installation: id)).IsUpdateOffered)];
        Check().ShouldBe(Check());
    }

    [Fact]
    public void pausing_customer_b_stops_its_offers_without_changing_other_audiences()
    {
        var scenario = new PrdScenario();
        var cohortMembers = InstallationIds(1_000)
            .Where(id => scenario.Resolve(Context(customer: "customer-b", installation: id)).IsUpdateOffered)
            .ToList();
        scenario.Builder.Replace(scenario.CustomerBDeployment, scenario.CustomerBDeployment with { State = DeploymentState.Paused });

        cohortMembers.ShouldAllBe(id => scenario.Resolve(Context("1.0.0", id, "customer-b", null, null, PlatformTarget.Windows)).Reason == DecisionReason.DeploymentPaused);
        scenario.Resolve(Context(customer: "customer-a")).OfferedVersion!.ToString().ShouldBe("1.1.0");
        scenario.Resolve(Context(groups: ["internal-qa"])).OfferedVersion!.ToString().ShouldBe("1.2.0");
    }

    [Fact]
    public void cancelled_deployments_no_longer_claim_their_audience()
    {
        var scenario = new PrdScenario();
        var cancelled = scenario.Builder.Build().Deployments.Single(d => d.Name == "Customer A");
        scenario.Builder.Replace(cancelled, cancelled with { State = DeploymentState.Cancelled });
        scenario.Resolve(Context(customer: "customer-a")).Deployment!.Name.ShouldBe("Default");
    }

    [Fact]
    public void draft_deployments_are_ignored()
    {
        var builder = new ScenarioBuilder();
        builder.Deploy("Draft", builder.Release("2.0.0"), builder.Audience(new EveryoneRule()), state: DeploymentState.Draft);
        UpdateResolver.Resolve(builder.Build(), Context()).Reason.ShouldBe(DecisionReason.NoMatchingDeployment);
    }

    [Fact]
    public void an_installation_already_on_a_newer_version_is_never_downgraded()
    {
        var decision = new PrdScenario().Resolve(Context(current: "1.2.0", customer: "customer-a"));
        decision.Reason.ShouldBe(DecisionReason.AlreadyUpToDate);
        decision.IsUpdateOffered.ShouldBeFalse();
    }

    [Fact]
    public void a_withdrawn_release_is_not_offered_and_does_not_fall_through()
    {
        var builder = new ScenarioBuilder();
        builder.Deploy("Default", builder.Release("1.1.0"), builder.Audience(new EveryoneRule()));
        builder.Deploy("A", builder.Release("1.2.0", ReleaseState.Withdrawn), builder.Audience(new CustomerRule(["customer-a"])));
        UpdateResolver.Resolve(builder.Build(), Context(customer: "customer-a")).Reason.ShouldBe(DecisionReason.ReleaseNotOfferable);
    }

    [Fact]
    public void a_deprecated_release_is_not_offered()
    {
        var builder = new ScenarioBuilder();
        builder.Deploy("Default", builder.Release("1.1.0", ReleaseState.Deprecated), builder.Audience(new EveryoneRule()));
        UpdateResolver.Resolve(builder.Build(), Context()).Reason.ShouldBe(DecisionReason.ReleaseNotOfferable);
    }

    [Fact]
    public void a_release_without_the_requested_platform_manifest_is_not_offered()
    {
        var builder = new ScenarioBuilder();
        builder.Deploy("Default", builder.Release("1.1.0", ReleaseState.Available, PlatformTarget.Windows), builder.Audience(new EveryoneRule()));
        UpdateResolver.Resolve(builder.Build(), Context(platform: PlatformTarget.MacOS)).Reason.ShouldBe(DecisionReason.ReleaseNotOnPlatform);
    }

    [Fact]
    public void an_exclusion_blocks_the_release_for_its_audience_only()
    {
        var builder = new ScenarioBuilder();
        var release = builder.Release("1.1.0");
        builder.Deploy("Default", release, builder.Audience(new EveryoneRule()));
        builder.Exclude("Broken on customer-a", builder.Audience(new CustomerRule(["customer-a"])), release);
        var snapshot = builder.Build();

        UpdateResolver.Resolve(snapshot, Context(customer: "customer-a")).Reason.ShouldBe(DecisionReason.ReleaseExcluded);
        UpdateResolver.Resolve(snapshot, Context(customer: "customer-b")).Reason.ShouldBe(DecisionReason.Offered);
    }

    [Fact]
    public void a_pin_overrides_deployments_for_its_audience()
    {
        var scenario = new PrdScenario();
        scenario.Builder.Pin("Hold QA on 1.1.0", scenario.Builder.Audience(new GroupRule(["internal-qa"])), scenario.V110);

        var decision = scenario.Resolve(Context(groups: ["internal-qa"]));
        decision.OfferedVersion!.ToString().ShouldBe("1.1.0");
        decision.AppliedPin!.Name.ShouldBe("Hold QA on 1.1.0");
        decision.Deployment.ShouldBeNull();
    }

    [Fact]
    public void the_most_specific_pin_wins()
    {
        var scenario = new PrdScenario();
        scenario.Builder.Pin("Customer pin", scenario.Builder.Audience(new CustomerRule(["customer-a"])), scenario.V110);
        scenario.Builder.Pin("Installation pin", scenario.Builder.Audience(new InstallationRule(["install-0001"])), scenario.V120);

        scenario.Resolve(Context(customer: "customer-a", installation: "install-0001")).AppliedPin!.Name.ShouldBe("Installation pin");
    }

    [Fact]
    public void an_exclusion_also_blocks_a_pinned_release()
    {
        var scenario = new PrdScenario();
        var qa = scenario.Builder.Audience(new GroupRule(["internal-qa"]));
        scenario.Builder.Pin("QA pin", qa, scenario.V120);
        scenario.Builder.Exclude("QA exclusion", qa, scenario.V120);
        scenario.Resolve(Context(groups: ["internal-qa"])).Reason.ShouldBe(DecisionReason.ReleaseExcluded);
    }

    [Fact]
    public void equal_specificity_is_broken_by_priority_then_version()
    {
        var builder = new ScenarioBuilder();
        var everyone = builder.Audience(new EveryoneRule());
        builder.Deploy("Low priority newer", builder.Release("1.3.0"), everyone, priority: 0);
        builder.Deploy("High priority older", builder.Release("1.2.0"), everyone, priority: 10);
        builder.Deploy("Low priority older", builder.Release("1.1.0"), everyone, priority: 0);

        UpdateResolver.Resolve(builder.Build(), Context()).Deployment!.Name.ShouldBe("High priority older");
    }

    [Fact]
    public void unknown_channel_yields_no_update()
    {
        var context = Context() with { Channel = Common.ChannelKey.From("nightly") };
        UpdateResolver.Resolve(new PrdScenario().Builder.Build(), context).Reason.ShouldBe(DecisionReason.UnknownChannel);
    }

    [Fact]
    public void decisions_explain_themselves()
    {
        var decision = new PrdScenario().Resolve(Context(customer: "customer-a"));
        decision.Trace.ShouldContain(step => step.Contains("Customer A", StringComparison.Ordinal));
        decision.Trace[^1].ShouldBe("Offering 1.1.0.");
    }

    [Fact]
    public void a_snapshot_survives_json_round_trip_with_identical_decisions()
    {
        var scenario = new PrdScenario();
        scenario.Builder.Pin("QA pin", scenario.Builder.Audience(new GroupRule(["internal-qa"]), new PlatformRule([PlatformTarget.MacOS]), new CurrentVersionRule("1.0.0", null)), scenario.V110);
        var snapshot = scenario.Builder.Build();

        var copy = JsonSerializer.Deserialize<ResolutionSnapshot>(JsonSerializer.Serialize(snapshot))!;

        foreach (var id in InstallationIds(200))
        {
            var context = Context(customer: "customer-b", installation: id, groups: ["internal-qa"]);
            UpdateResolver.Resolve(copy, context).Reason.ShouldBe(UpdateResolver.Resolve(snapshot, context).Reason);
        }
    }
}
