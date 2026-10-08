using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Releaser.Server.Tests.Infrastructure;

/// <summary>Plays the publisher's backend: holds the ES256 private key and signs identity-context tokens.</summary>
public sealed class ContextTokens : IDisposable
{
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public string PublicKeyPem => _key.ExportSubjectPublicKeyInfoPem();

    public string Sign(string audience, string? customer = null, string? user = null, string[]? groups = null, string? installation = null,
        TimeSpan? lifetime = null, DateTime? issuedAt = null)
    {
        var issued = issuedAt ?? DateTime.UtcNow;
        var claims = new Dictionary<string, object>();
        if (customer is not null)
        {
            claims["tid"] = customer;
        }
        if (user is not null)
        {
            claims["sub"] = user;
        }
        if (groups is not null)
        {
            claims["grp"] = groups;
        }
        if (installation is not null)
        {
            claims["iid"] = installation;
        }
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Audience = audience,
            IssuedAt = issued,
            NotBefore = issued,
            Expires = issued + (lifetime ?? TimeSpan.FromMinutes(30)),
            Claims = claims,
            SigningCredentials = new SigningCredentials(new ECDsaSecurityKey(_key), SecurityAlgorithms.EcdsaSha256),
        });
    }

    public void Dispose() => _key.Dispose();
}
