using Releaser.Domain.Common;
using Releaser.Domain.Targeting;

namespace Releaser.Electron.Tests;

public sealed class FeedFileNameTests
{
    [Theory]
    [InlineData("latest.yml", "latest", PlatformTarget.Windows)]
    [InlineData("latest-mac.yml", "latest", PlatformTarget.MacOS)]
    [InlineData("latest-linux.yml", "latest", PlatformTarget.LinuxX64)]
    [InlineData("latest-linux-arm64.yml", "latest", PlatformTarget.LinuxArm64)]
    [InlineData("latest-linux-armv7l.yml", "latest", PlatformTarget.LinuxArmv7l)]
    [InlineData("beta-mac.yml", "beta", PlatformTarget.MacOS)]
    [InlineData("early-access-linux.yml", "early-access", PlatformTarget.LinuxX64)]
    public void parses_electron_updater_channel_files(string fileName, string channel, PlatformTarget platform)
    {
        FeedFileName.TryParse(fileName, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(new FeedFileName(channel, platform));
        FeedFileName.For(platform, channel).ShouldBe(fileName);
    }

    [Theory]
    [InlineData("latest.json")]
    [InlineData(".yml")]
    [InlineData("-mac.yml")]
    public void rejects_non_channel_files(string fileName)
    {
        FeedFileName.TryParse(fileName, out _).ShouldBeFalse();
    }

    [Fact]
    public void latest_maps_to_the_application_default_channel()
    {
        var stable = ChannelKey.From("stable");
        new FeedFileName("latest", PlatformTarget.Windows).ResolveChannel(stable).ShouldBe(stable);
        new FeedFileName("beta", PlatformTarget.Windows).ResolveChannel(stable).ShouldBe(ChannelKey.From("beta"));
        new FeedFileName("Bad_Channel", PlatformTarget.Windows).ResolveChannel(stable).ShouldBeNull();
    }

    [Theory]
    [InlineData("stable", true)]
    [InlineData("beta", true)]
    [InlineData("latest", false)]
    [InlineData("qa-mac", false)]
    [InlineData("qa-linux", false)]
    public void channel_keys_must_not_collide_with_platform_suffixes(string key, bool allowed)
    {
        FeedFileName.IsUnambiguousChannelKey(key).ShouldBe(allowed);
    }
}
