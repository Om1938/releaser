using Releaser.Domain.Common;
using Releaser.Domain.Targeting;

namespace Releaser.Electron;

/// <summary>
/// Maps electron-updater channel file names (<c>{channel}{suffix}.yml</c>) to platform targets.
/// Suffixes mirror electron-updater's <c>Provider.getChannelFilePrefix</c> (ADR 0002).
/// </summary>
public sealed record FeedFileName(string ElectronChannel, PlatformTarget Platform)
{
    /// <summary>electron-updater's default channel name; maps to the application's default channel.</summary>
    public const string DefaultElectronChannel = "latest";

    private static readonly (string Suffix, PlatformTarget Platform)[] Suffixes =
    [
        ("-linux-arm64", PlatformTarget.LinuxArm64),
        ("-linux-armv7l", PlatformTarget.LinuxArmv7l),
        ("-linux", PlatformTarget.LinuxX64),
        ("-mac", PlatformTarget.MacOS),
    ];

    public static bool TryParse(string fileName, out FeedFileName? feedFile)
    {
        feedFile = null;
        if (!fileName.EndsWith(".yml", StringComparison.Ordinal))
        {
            return false;
        }
        var stem = fileName[..^4];
        var (suffix, platform) = Suffixes.FirstOrDefault(s => stem.EndsWith(s.Suffix, StringComparison.Ordinal));
        var channel = suffix is null ? stem : stem[..^suffix.Length];
        if (channel.Length == 0)
        {
            return false;
        }
        feedFile = new FeedFileName(channel, suffix is null ? PlatformTarget.Windows : platform);
        return true;
    }

    /// <summary>The file name electron-builder publishes for a platform (on the default <c>latest</c> channel).</summary>
    public static string For(PlatformTarget platform, string electronChannel = DefaultElectronChannel) =>
        electronChannel + (platform == PlatformTarget.Windows ? string.Empty : Suffixes.Single(s => s.Platform == platform).Suffix) + ".yml";

    /// <summary>Resolves the platform channel requested by an updater: <c>latest</c> means the application's default channel.</summary>
    public ChannelKey? ResolveChannel(ChannelKey defaultChannel)
    {
        if (ElectronChannel == DefaultElectronChannel)
        {
            return defaultChannel;
        }
        try
        {
            return ChannelKey.From(ElectronChannel);
        }
        catch (DomainRuleException)
        {
            return null;
        }
    }

    /// <summary>Channel keys must not end with a platform suffix, or electron-updater file names would be ambiguous.</summary>
    public static bool IsUnambiguousChannelKey(string channelKey) =>
        channelKey != DefaultElectronChannel && !Suffixes.Any(s => channelKey.EndsWith(s.Suffix, StringComparison.Ordinal));
}
