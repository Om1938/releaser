using Releaser.Domain.Common;
using Releaser.Domain.Resolution;

namespace Releaser.Server.Infrastructure.Caching;

/// <summary>Everything the update feed needs for one application at one configuration version. Immutable and cacheable.</summary>
public sealed record FeedSnapshot(AppId AppId, ApplicationKey Key, ResolutionSnapshot Resolution, IReadOnlyList<ContextKeyMaterial> ContextKeys);

/// <summary>An active ES256 public key used to verify identity-context tokens.</summary>
public sealed record ContextKeyMaterial(Guid Id, string PublicKeyPem);
