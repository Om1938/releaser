using System.Security.Cryptography;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Releaser.Domain.Common;
using Releaser.Server.Features.Audit;
using Releaser.Server.Infrastructure.Http;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Features.ContextKeys;

public sealed record ContextKeyResponse(Guid Id, string Name, string PublicKeyPem, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt)
{
    public static ContextKeyResponse From(ContextSigningKey k) => new(k.Id, k.Name, k.PublicKeyPem, k.CreatedAt, k.RevokedAt);
}

public sealed record RegisterContextKeyRequest(string Name, string PublicKeyPem);

internal sealed class RegisterContextKeyValidator : AbstractValidator<RegisterContextKeyRequest>
{
    public RegisterContextKeyValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.PublicKeyPem).NotEmpty().MaximumLength(4096).Must(BeP256PublicKey)
            .WithMessage("Provide a PEM-encoded P-256 (ES256) public key ('-----BEGIN PUBLIC KEY-----'). Never upload a private key.");
    }

    private static bool BeP256PublicKey(string pem)
    {
        if (pem.Contains("PRIVATE KEY", StringComparison.Ordinal))
        {
            return false;
        }
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(pem);
            return ecdsa.KeySize == 256;
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException)
        {
            return false;
        }
    }
}

internal static class ContextKeyEndpoints
{
    public static RouteGroupBuilder MapContextKeys(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/applications/{appId:guid}/context-keys").WithTags("Context keys");
        group.MapGet("/", ListAsync).WithName("ListContextKeys");
        group.MapPost("/", RegisterAsync).WithName("RegisterContextKey").Validate<RegisterContextKeyRequest>().RequiresReleaseManager();
        group.MapPost("/{keyId:guid}/revoke", RevokeAsync).WithName("RevokeContextKey").RequiresReleaseManager();
        return admin;
    }

    private static async Task<Ok<List<ContextKeyResponse>>> ListAsync(Guid appId, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        var keys = await db.ContextSigningKeys.AsNoTracking().Where(k => k.AppId == new AppId(appId)).OrderByDescending(k => k.CreatedAt).ToListAsync(cancellationToken);
        return TypedResults.Ok(keys.Select(ContextKeyResponse.From).ToList());
    }

    private static async Task<Results<Created<ContextKeyResponse>, NotFound>> RegisterAsync(
        Guid appId, RegisterContextKeyRequest request, ReleaserDbContext db, IAuditLog audit, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (!await db.Applications.AnyAsync(a => a.Id == new AppId(appId), cancellationToken))
        {
            return TypedResults.NotFound();
        }
        var key = ContextSigningKey.Register(new AppId(appId), request.Name, request.PublicKeyPem.Trim(), clock.GetUtcNow());
        db.ContextSigningKeys.Add(key);
        audit.Record(new AuditRecord("context_key.registered", "context_key", key.Id.ToString(), appId, $"name={key.Name}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/admin/v1/applications/{appId}/context-keys/{key.Id}", ContextKeyResponse.From(key));
    }

    private static async Task<Results<Ok<ContextKeyResponse>, NotFound>> RevokeAsync(
        Guid appId, Guid keyId, ReleaserDbContext db, IAuditLog audit, TimeProvider clock, CancellationToken cancellationToken)
    {
        var key = await db.ContextSigningKeys.SingleOrDefaultAsync(k => k.AppId == new AppId(appId) && k.Id == keyId, cancellationToken);
        if (key is null)
        {
            return TypedResults.NotFound();
        }
        key.Revoke(clock.GetUtcNow());
        audit.Record(new AuditRecord("context_key.revoked", "context_key", key.Id.ToString(), appId, $"name={key.Name}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ContextKeyResponse.From(key));
    }
}
