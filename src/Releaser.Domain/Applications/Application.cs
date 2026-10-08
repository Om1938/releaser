using Releaser.Domain.Common;
using Releaser.Domain.Targeting;

namespace Releaser.Domain.Applications;

/// <summary>An independently managed application with its own release history.</summary>
public sealed class Application
{
    private Application() { } // EF Core

    public AppId Id { get; private set; }
    public ApplicationKey Key { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public ChannelKey DefaultChannel { get; private set; } = null!;

    /// <summary>Platforms this application ships to. Releases can only carry manifests for these; others are never offered.</summary>
    public IReadOnlyList<PlatformTarget> SupportedPlatforms { get; private set; } = [];

    /// <summary>Monotonic version of everything that affects resolution; incremented atomically by persistence (ADR 0008).</summary>
    public long ConfigVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Application Create(ApplicationKey key, string name, string? description, ChannelKey defaultChannel,
        IEnumerable<PlatformTarget> supportedPlatforms, DateTimeOffset now)
    {
        var application = new Application
        {
            Id = AppId.New(),
            Key = key,
            Name = name,
            Description = description,
            DefaultChannel = defaultChannel,
            ConfigVersion = 1,
            CreatedAt = now,
        };
        application.ChangeSupportedPlatforms(supportedPlatforms);
        return application;
    }

    /// <summary>Replaces the supported platform set. Dropping a platform stops new offers to it (ADR 0006).</summary>
    public void ChangeSupportedPlatforms(IEnumerable<PlatformTarget> platforms)
    {
        List<PlatformTarget> normalized = [.. platforms.Distinct().Order()];
        if (normalized.Count == 0)
        {
            throw new DomainRuleException("application.no_platforms", "An application must support at least one platform.");
        }
        SupportedPlatforms = normalized;
    }

    public void EnsureSupports(PlatformTarget platform)
    {
        if (!SupportedPlatforms.Contains(platform))
        {
            throw new DomainRuleException("release.platform_not_supported",
                $"{Name} does not support {platform}. Add it under the application's supported platforms first.");
        }
    }

    public void Rename(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    public void ChangeDefaultChannel(ChannelKey channel) => DefaultChannel = channel;
}
