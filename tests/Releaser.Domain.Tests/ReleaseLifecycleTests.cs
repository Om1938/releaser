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

    [Fact]
    public void a_platform_can_be_added_later_and_platforms_stay_sorted()
    {
        var release = NewRelease();
        release.AddPlatform(PlatformTarget.MacOS);
        release.Platforms.ShouldBe([PlatformTarget.Windows, PlatformTarget.MacOS]);
    }

    [Fact]
    public void adding_a_platform_the_release_already_has_is_rejected()
    {
        var release = NewRelease();
        Should.Throw<DomainRuleException>(() => release.AddPlatform(PlatformTarget.Windows)).Code.ShouldBe("release.platform_exists");
    }

    [Fact]
    public void a_withdrawn_release_cannot_gain_platforms()
    {
        var release = NewRelease();
        release.Withdraw();
        Should.Throw<DomainRuleException>(() => release.AddPlatform(PlatformTarget.MacOS)).Code.ShouldBe("release.withdrawn");
    }

    [Fact]
    public void a_deprecated_release_can_still_gain_platforms()
    {
        var release = NewRelease();
        release.Deprecate();
        release.AddPlatform(PlatformTarget.LinuxX64);
        release.Platforms.ShouldContain(PlatformTarget.LinuxX64);
    }

    [Theory]
    [InlineData("1.2.0")]
    [InlineData(" 1.2.0 ")]
    public void obliteration_is_confirmed_by_typing_the_exact_version(string typed)
    {
        NewRelease().ConfirmObliteration(typed);
    }

    [Theory]
    [InlineData("1.2")]
    [InlineData("1.2.1")]
    [InlineData("v1.2.0")]
    [InlineData("")]
    [InlineData(null)]
    public void obliteration_with_a_wrong_confirmation_is_refused(string? typed)
    {
        Should.Throw<DomainRuleException>(() => NewRelease().ConfirmObliteration(typed)).Code.ShouldBe("release.obliteration_unconfirmed");
    }

    [Fact]
    public void withdrawn_releases_can_be_obliterated()
    {
        var release = NewRelease();
        release.Withdraw();
        release.ConfirmObliteration("1.2.0");
    }
}
