using Releaser.Domain.Common;
using Releaser.Domain.Targeting;

namespace Releaser.Domain.Releases;

/// <summary>
/// A registered, externally built application version. Identity (application, version) is immutable;
/// platforms are append-only (a platform's manifest can be added later but never replaced or removed).
/// </summary>
public sealed class Release : IApplicationScoped
{
    private readonly List<ChannelKey> _channels = [];
    private readonly List<PlatformTarget> _platforms = [];

    private Release() { } // EF Core

    public ReleaseId Id { get; private set; }
    public AppId AppId { get; private set; }
    public SemanticVersion Version { get; private set; } = null!;
    public string? Title { get; private set; }
    public ReleaseState State { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }
    public IReadOnlyList<ChannelKey> Channels => _channels;
    public IReadOnlyList<PlatformTarget> Platforms => _platforms;

    public static Release Register(AppId applicationId, SemanticVersion version, string? title, IEnumerable<PlatformTarget> platforms, DateTimeOffset now)
    {
        var release = new Release
        {
            Id = ReleaseId.New(),
            AppId = applicationId,
            Version = version,
            Title = title,
            State = ReleaseState.Available,
            RegisteredAt = now,
        };
        release._platforms.AddRange(platforms.Distinct().Order());
        if (release._platforms.Count == 0)
        {
            throw new DomainRuleException("release.no_platforms", "A release needs at least one platform manifest.");
        }
        return release;
    }

    public bool IsOfferable => State == ReleaseState.Available;

    public void Rename(string? title) => Title = title;

    public void AssignChannel(ChannelKey channel)
    {
        EnsureNotWithdrawn();
        if (!_channels.Contains(channel))
        {
            _channels.Add(channel);
        }
    }

    public void UnassignChannel(ChannelKey channel) => _channels.Remove(channel);

    /// <summary>Makes the release available on one more platform, e.g. when the macOS build ships after Windows.</summary>
    public void AddPlatform(PlatformTarget platform)
    {
        EnsureNotWithdrawn();
        if (_platforms.Contains(platform))
        {
            throw new DomainRuleException("release.platform_exists", $"Release {Version} already has a {platform} manifest; registered manifests are immutable.");
        }
        _platforms.Add(platform);
        _platforms.Sort();
    }

    public void Deprecate()
    {
        if (State != ReleaseState.Available)
        {
            throw new DomainRuleException("release.invalid_transition", $"Only available releases can be deprecated (current: {State}).");
        }
        State = ReleaseState.Deprecated;
    }

    public void Withdraw()
    {
        EnsureNotWithdrawn();
        State = ReleaseState.Withdrawn;
    }

    /// <summary>
    /// Guards the irreversible removal of a release (issue #12): the operator must repeat the exact version.
    /// Allowed in every state; installations that already received this version keep it.
    /// </summary>
    public void ConfirmObliteration(string? typedVersion)
    {
        if (typedVersion?.Trim() != Version.Value)
        {
            throw new DomainRuleException("release.obliteration_unconfirmed",
                $"Type the exact version ({Version}) to confirm that this release and everything attached to it will be permanently deleted.");
        }
    }

    private void EnsureNotWithdrawn()
    {
        if (State == ReleaseState.Withdrawn)
        {
            throw new DomainRuleException("release.withdrawn", "The release is withdrawn; withdrawal is permanent.");
        }
    }
}
