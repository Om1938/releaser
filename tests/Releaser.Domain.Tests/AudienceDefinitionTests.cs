using Releaser.Domain.Common;
using Releaser.Domain.Targeting;

namespace Releaser.Domain.Tests;

public sealed class AudienceDefinitionTests
{
    [Fact]
    public void when_any_include_matches_the_most_specific_matching_rule_sets_specificity()
    {
        var audience = new AudienceDefinition(AudienceId.New(),
            [new CustomerRule(["customer-a"]), new GroupRule(["qa"])], []);

        audience.Match(ScenarioBuilder.Context(customer: "customer-a", groups: ["qa"])).ShouldBe(AudienceSpecificity.Group);
        audience.Match(ScenarioBuilder.Context(customer: "customer-a")).ShouldBe(AudienceSpecificity.Customer);
    }

    [Fact]
    public void when_an_exclude_rule_matches_the_audience_does_not_match()
    {
        var audience = new AudienceDefinition(AudienceId.New(),
            [new EveryoneRule()], [new InstallationRule(["install-0001"])]);

        audience.Match(ScenarioBuilder.Context(installation: "install-0001")).ShouldBeNull();
        audience.Match(ScenarioBuilder.Context(installation: "install-0002")).ShouldBe(AudienceSpecificity.Everyone);
    }

    [Fact]
    public void an_audience_without_include_rules_matches_nobody()
    {
        new AudienceDefinition(AudienceId.New(), [], []).Match(ScenarioBuilder.Context()).ShouldBeNull();
    }

    [Fact]
    public void current_version_rule_matches_half_open_range()
    {
        var rule = new CurrentVersionRule("1.0.0", "1.2.0");
        rule.Matches(ScenarioBuilder.Context(current: "1.0.0")).ShouldBeTrue();
        rule.Matches(ScenarioBuilder.Context(current: "1.1.9")).ShouldBeTrue();
        rule.Matches(ScenarioBuilder.Context(current: "1.2.0")).ShouldBeFalse();
    }

    [Fact]
    public void platform_rule_matches_only_listed_platforms()
    {
        var rule = new PlatformRule([PlatformTarget.MacOS]);
        rule.Matches(ScenarioBuilder.Context(platform: PlatformTarget.MacOS)).ShouldBeTrue();
        rule.Matches(ScenarioBuilder.Context(platform: PlatformTarget.Windows)).ShouldBeFalse();
    }

    [Fact]
    public void anonymous_identity_never_matches_customer_user_or_group_rules()
    {
        var context = ScenarioBuilder.Context();
        new CustomerRule(["customer-a"]).Matches(context).ShouldBeFalse();
        new UserRule(["user-1"]).Matches(context).ShouldBeFalse();
        new GroupRule(["qa"]).Matches(context).ShouldBeFalse();
    }
}
