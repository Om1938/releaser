using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Releaser.Domain.Targeting;
using Releaser.Server.Infrastructure.Caching;

namespace Releaser.Server.Infrastructure.Auth;

/// <summary>Outcome of validating an identity-context token. A rejected token yields an anonymous identity, never an error to the client.</summary>
public sealed record ContextValidation(VerifiedIdentity Identity, string? RejectionReason)
{
    public static ContextValidation Absent { get; } = new(VerifiedIdentity.Anonymous, null);
}

/// <summary>Verifies publisher-signed ES256 identity-context tokens (ADR 0004).</summary>
public sealed class ContextTokenValidator(TimeProvider clock, IOptions<ContextTokenOptions> options)
{
    private const int MaxTokenLength = 8192;
    private static readonly JsonWebTokenHandler Handler = new() { MapInboundClaims = false };
    private readonly ConcurrentDictionary<Guid, ECDsaSecurityKey> _keys = new();

    public async Task<ContextValidation> ValidateAsync(string? authorization, FeedSnapshot snapshot, InstallationId? installation)
    {
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return ContextValidation.Absent;
        }
        var token = authorization["Bearer ".Length..].Trim();
        if (token.Length > MaxTokenLength || snapshot.ContextKeys.Count == 0)
        {
            return Rejected(snapshot.ContextKeys.Count == 0 ? "no context signing keys are registered" : "token too long");
        }
        var result = await Handler.ValidateTokenAsync(token, Parameters(snapshot));
        if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt)
        {
            return Rejected(result.Exception?.GetType().Name ?? "invalid token");
        }
        return Interpret(jwt, installation);
    }

    private ContextValidation Interpret(JsonWebToken jwt, InstallationId? installation)
    {
        var lifetime = jwt.ValidTo - (jwt.IssuedAt == DateTime.MinValue ? jwt.ValidFrom : jwt.IssuedAt);
        if (jwt.IssuedAt == DateTime.MinValue || lifetime > TimeSpan.FromHours(options.Value.MaxLifetimeHours))
        {
            return Rejected("token must carry iat and a lifetime within the configured maximum");
        }
        if (jwt.TryGetPayloadValue<string>("iid", out var tokenInstallation) && tokenInstallation != installation?.Value)
        {
            return Rejected("token is bound to a different installation");
        }
        var identity = new VerifiedIdentity(
            jwt.TryGetPayloadValue<string>("sub", out var user) ? user : null,
            jwt.TryGetPayloadValue<string>("tid", out var customer) ? customer : null,
            ReadGroups(jwt),
            ReadAttributes(jwt));
        return new ContextValidation(identity, null);
    }

    private TokenValidationParameters Parameters(FeedSnapshot snapshot) => new()
    {
        ValidAudience = snapshot.Key.Value,
        ValidateIssuer = false,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
        IssuerSigningKeys = snapshot.ContextKeys.Select(ToSecurityKey),
        ClockSkew = TimeSpan.FromSeconds(options.Value.ClockSkewSeconds),
        TryAllIssuerSigningKeys = true,
        LifetimeValidator = (notBefore, expires, _, parameters) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            return expires is not null
                && now <= expires.Value + parameters.ClockSkew
                && (notBefore is null || now >= notBefore.Value - parameters.ClockSkew);
        },
    };

    private ECDsaSecurityKey ToSecurityKey(ContextKeyMaterial key) =>
        _keys.GetOrAdd(key.Id, _ =>
        {
            var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(key.PublicKeyPem);
            return new ECDsaSecurityKey(ecdsa) { KeyId = key.Id.ToString() };
        });

    private static HashSet<string> ReadGroups(JsonWebToken jwt)
    {
        if (jwt.TryGetPayloadValue<string[]>("grp", out var groups))
        {
            return [.. groups];
        }
        return jwt.TryGetPayloadValue<string>("grp", out var single) ? [single] : [];
    }

    private static Dictionary<string, string> ReadAttributes(JsonWebToken jwt)
    {
        if (!jwt.TryGetPayloadValue<JsonElement>("attrs", out var attrs) || attrs.ValueKind != JsonValueKind.Object)
        {
            return [];
        }
        return attrs.EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(property => property.Name, property => property.Value.GetString()!, StringComparer.Ordinal);
    }

    private static ContextValidation Rejected(string reason) => new(VerifiedIdentity.Anonymous, reason);
}
