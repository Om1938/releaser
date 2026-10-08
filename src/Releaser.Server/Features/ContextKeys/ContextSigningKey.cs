using Releaser.Domain.Common;

namespace Releaser.Server.Features.ContextKeys;

/// <summary>ES256 public key the publisher's backend uses to sign identity-context tokens (ADR 0004). Private keys never reach the platform.</summary>
public sealed class ContextSigningKey : IApplicationScoped
{
    private ContextSigningKey() { } // EF Core

    public Guid Id { get; private set; }
    public AppId AppId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string PublicKeyPem { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null;

    public static ContextSigningKey Register(AppId appId, string name, string publicKeyPem, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        AppId = appId,
        Name = name,
        PublicKeyPem = publicKeyPem,
        CreatedAt = now,
    };

    public void Revoke(DateTimeOffset now)
    {
        if (RevokedAt is not null)
        {
            throw new DomainRuleException("context_key.revoked", "The key is already revoked.");
        }
        RevokedAt = now;
    }
}
