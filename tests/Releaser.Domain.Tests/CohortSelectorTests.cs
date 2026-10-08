using Releaser.Domain.Rollouts;
using Releaser.Domain.Targeting;

namespace Releaser.Domain.Tests;

public sealed class CohortSelectorTests
{
    private static readonly InstallationId[] Installations =
        [.. Enumerable.Range(0, 10_000).Select(i => InstallationId.From($"installation-{i:D6}"))];

    [Fact]
    public void when_checked_repeatedly_membership_never_changes()
    {
        var percentage = RolloutPercentage.From(20m);
        var first = Installations.Select(id => CohortSelector.Contains("salt", percentage, id)).ToList();
        var second = Installations.Select(id => CohortSelector.Contains("salt", percentage, id)).ToList();
        second.ShouldBe(first);
    }

    [Fact]
    public void when_rollout_is_20_percent_about_20_percent_of_installations_are_eligible()
    {
        var eligible = Installations.Count(id => CohortSelector.Contains("salt", RolloutPercentage.From(20m), id));
        eligible.ShouldBeInRange(1_850, 2_150);
    }

    [Fact]
    public void when_raising_from_20_to_50_percent_every_earlier_member_stays_eligible()
    {
        var at20 = Installations.Where(id => CohortSelector.Contains("salt", RolloutPercentage.From(20m), id));
        at20.ShouldAllBe(id => CohortSelector.Contains("salt", RolloutPercentage.From(50m), id));
    }

    [Fact]
    public void different_salts_select_different_cohorts()
    {
        var a = Installations.Where(id => CohortSelector.Contains("salt-a", RolloutPercentage.From(20m), id)).ToHashSet();
        var b = Installations.Where(id => CohortSelector.Contains("salt-b", RolloutPercentage.From(20m), id)).ToHashSet();
        a.Intersect(b).Count().ShouldBeLessThan(600);
    }

    [Fact]
    public void without_an_installation_id_only_full_rollouts_include_the_client()
    {
        CohortSelector.Contains("salt", RolloutPercentage.From(99.99m), null).ShouldBeFalse();
        CohortSelector.Contains("salt", RolloutPercentage.Full, null).ShouldBeTrue();
    }

    [Fact]
    public void zero_percent_includes_nobody()
    {
        Installations.ShouldAllBe(id => !CohortSelector.Contains("salt", RolloutPercentage.From(0m), id));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    [InlineData(12.345)]
    public void invalid_percentages_are_rejected(decimal percent)
    {
        Should.Throw<Common.DomainRuleException>(() => RolloutPercentage.From(percent));
    }
}
