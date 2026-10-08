using System.Data;
using Microsoft.EntityFrameworkCore;
using Releaser.Domain.Common;
using Releaser.Domain.Resolution;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Infrastructure.Caching;

/// <summary>Reads a consistent snapshot of one application from PostgreSQL (single REPEATABLE READ transaction).</summary>
internal sealed class FeedSnapshotLoader(ReleaserDbContext db)
{
    public async Task<FeedSnapshot> LoadAsync(AppId appId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var application = await db.Applications.AsNoTracking().SingleAsync(a => a.Id == appId, cancellationToken);
        var channels = await db.Channels.AsNoTracking().Where(c => c.AppId == appId).Select(c => c.Key).ToListAsync(cancellationToken);
        var releases = await db.Releases.AsNoTracking().Where(r => r.AppId == appId).ToListAsync(cancellationToken);
        var deployments = await db.Deployments.AsNoTracking().Where(d => d.AppId == appId).ToListAsync(cancellationToken);
        var audiences = await db.Audiences.AsNoTracking().Where(a => a.AppId == appId).ToListAsync(cancellationToken);
        var policies = await db.Policies.AsNoTracking().Where(p => p.AppId == appId).ToListAsync(cancellationToken);
        var keys = await db.ContextSigningKeys.AsNoTracking()
            .Where(k => k.AppId == appId && k.RevokedAt == null)
            .Select(k => new ContextKeyMaterial(k.Id, k.PublicKeyPem))
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var resolution = new ResolutionSnapshot(
            application.ConfigVersion,
            application.DefaultChannel,
            channels,
            [.. releases.Select(ReleaseCandidate.From)],
            [.. deployments.Select(DeploymentRule.From).Where(d => d.ClaimsAudience)],
            [.. audiences.Select(a => a.ToDefinition())],
            [.. policies.Where(p => p.Kind == Domain.Policies.PolicyKind.Pin)
                .Select(p => new PinRule(p.Id, p.Name, p.AudienceId, p.ReleaseId, p.Priority, p.CreatedAt))],
            [.. policies.Where(p => p.Kind == Domain.Policies.PolicyKind.Exclusion)
                .Select(p => new ExclusionRule(p.Id, p.Name, p.AudienceId, p.ReleaseId))]);
        return new FeedSnapshot(application.Id, application.Key, resolution, keys);
    }
}
