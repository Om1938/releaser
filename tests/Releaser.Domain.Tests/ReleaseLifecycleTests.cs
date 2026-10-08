using Releaser.Domain.Common;
using Releaser.Domain.Releases;
using Releaser.Domain.Targeting;

namespace Releaser.Domain.Tests;

public sealed class ReleaseLifecycleTests
{
    private static Release NewRelease() =>
        Release.Register(AppId.New(), SemanticVersion.Parse("1.2.0"), null, [PlatformTarget.Windows], DateTimeOffset.UnixEpoch);

    [Fact]
    public void a_registered_release_is_available_and_offerable()
    {
        var release = NewRelease();
        release.State.ShouldBe(ReleaseState.Available);
        release.IsOfferable.ShouldBeTrue();
    }

    [Fact]
    public void withdrawal_is_permanent()
    {
        var release = NewRelease();
        release.Withdraw();
        Should.Throw<DomainRuleException>(release.Withdraw);
        Should.Throw<DomainRuleException>(release.Deprecate);
        Should.Throw<DomainRuleException>(() => release.AssignChannel(ChannelKey.From("beta")));
    }

    [Fact]
    public void a_deprecated_release_can_still_be_withdrawn()
    {
        var release = NewRelease();
        release.Deprecate();
        release.Withdraw();
        release.State.ShouldBe(ReleaseState.Withdrawn);
    }

    [Fact]
    public void assigning_the_same_channel_twice_keeps_one_assignment()
    {
        var release = NewRelease();
        release.AssignChannel(ChannelKey.From("stable"));
        release.AssignChannel(ChannelKey.From("stable"));
        release.Channels.Count.ShouldBe(1);
    }

    [Fact]
    public void a_release_without_platforms_is_rejected()
    {
        Should.Throw<DomainRuleException>(() =>
            Release.Register(AppId.New(), SemanticVersion.Parse("1.0.0"), null, [], DateTimeOffset.UnixEpoch));
    }
}
