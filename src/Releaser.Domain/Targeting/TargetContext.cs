using Releaser.Domain.Common;

namespace Releaser.Domain.Targeting;

/// <summary>Everything known about the installation asking for an update.</summary>
public sealed record TargetContext(
    ChannelKey Channel,
    PlatformTarget Platform,
    SemanticVersion CurrentVersion,
    InstallationId? Installation,
    VerifiedIdentity Identity);
