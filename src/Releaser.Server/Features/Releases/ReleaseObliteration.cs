using Microsoft.EntityFrameworkCore;
using Releaser.Domain.Common;
using Releaser.Domain.Deployments;
using Releaser.Domain.Policies;
using Releaser.Domain.Releases;
using Releaser.Domain.Targeting;
using Releaser.Server.Features.Audit;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Features.Releases;

public sealed record ObliterationDeployment(Guid Id, string Name, DeploymentState State, string Channel, string AudienceName, bool IsLive);

/// <summary>Everything that would be permanently deleted with a release (issue #12).</summary>
public sealed record ObliterationImpact(
    Guid ReleaseId, string Version, ReleaseState State, IReadOnlyList<PlatformTarget> Platforms,
    IReadOnlyList<ObliterationDeployment> Deployments, int Pins, int Exclusions, bool HasReleaseNote, int ReleaseNoteRevisions);

/// <summary>Permanently removes a release and everything attached to it, keeping only the audit trail (ADR 0014).</summary>
internal sealed class ReleaseObliteration(ReleaserDbContext db, IAuditLog audit)
{
    private const int MaxAuditDetails = 4000;

    public async Task<ObliterationImpact?> ImpactAsync(AppId appId, ReleaseId releaseId, CancellationToken cancellationToken)
    {
        var release = await db.Releases.AsNoTracking().SingleOrDefaultAsync(r => r.AppId == appId && r.Id == releaseId, cancellationToken);
        if (release is null)
        {
            return null;
        }
        var deployments = await (
            from deployment in db.Deployments.AsNoTracking().Where(d => d.ReleaseId == releaseId)
            join audience in db.Audiences.AsNoTracking() on deployment.AudienceId equals audience.Id
            select new { deployment, audience.Name })
            .ToListAsync(cancellationToken);
        var policies = await db.Policies.AsNoTracking().Where(p => p.ReleaseId == releaseId).Select(p => p.Kind).ToListAsync(cancellationToken);
        var noteId = await db.ReleaseNotes.Where(n => n.ReleaseId == releaseId).Select(n => (Guid?)n.Id).SingleOrDefaultAsync(cancellationToken);
        var revisions = noteId is null ? 0 : await db.ReleaseNoteRevisions.CountAsync(r => r.ReleaseNoteId == noteId, cancellationToken);
        return new ObliterationImpact(
            release.Id.Value, release.Version.Value, release.State, release.Platforms,
            [.. deployments.Select(d => new ObliterationDeployment(d.deployment.Id.Value, d.deployment.Name, d.deployment.State, d.deployment.Channel.Value, d.Name,
                d.deployment.State is DeploymentState.Active or DeploymentState.Paused or DeploymentState.Completed))],
            policies.Count(kind => kind == PolicyKind.Pin), policies.Count(kind => kind == PolicyKind.Exclusion), noteId is not null, revisions);
    }

    /// <summary>
    /// Deletes the release, its manifests, notes (and revisions), deployments and policies in one transaction.
    /// Returns false when the release does not exist. The application's configuration version is bumped automatically.
    /// </summary>
    public async Task<bool> ObliterateAsync(AppId appId, ReleaseId releaseId, string? confirmVersion, CancellationToken cancellationToken)
    {
        var release = await db.Releases.SingleOrDefaultAsync(r => r.AppId == appId && r.Id == releaseId, cancellationToken);
        if (release is null)
        {
            return false;
        }
        release.ConfirmObliteration(confirmVersion);

        var manifests = await db.ReleaseManifests.Where(m => m.ReleaseId == releaseId).ToListAsync(cancellationToken);
        var note = await db.ReleaseNotes.SingleOrDefaultAsync(n => n.ReleaseId == releaseId, cancellationToken);
        var revisions = note is null ? [] : await db.ReleaseNoteRevisions.Where(r => r.ReleaseNoteId == note.Id).ToListAsync(cancellationToken);
        var deployments = await db.Deployments.Where(d => d.ReleaseId == releaseId).ToListAsync(cancellationToken);
        var policies = await db.Policies.Where(p => p.ReleaseId == releaseId).ToListAsync(cancellationToken);

        db.ReleaseNoteRevisions.RemoveRange(revisions);
        if (note is not null)
        {
            db.ReleaseNotes.Remove(note);
        }
        db.Policies.RemoveRange(policies);
        db.Deployments.RemoveRange(deployments);
        db.ReleaseManifests.RemoveRange(manifests);
        db.Releases.Remove(release);

        var details = $"version={release.Version}; state={release.State}; platforms={string.Join(',', release.Platforms)}; " +
            $"deployments={deployments.Count} ({string.Join(", ", deployments.Select(d => $"{d.Name}:{d.State}"))}); " +
            $"policies={policies.Count}; note={(note is null ? "none" : $"rev {note.Revision}")}; " +
            $"manifests={string.Join(" ", manifests.Select(m => $"{m.Platform}={m.SourceUrl}#sha256:{m.SourceSha256}"))}";
        audit.Record(new AuditRecord("release.obliterated", "release", release.Id.ToString(), appId.Value,
            details.Length <= MaxAuditDetails ? details : details[..(MaxAuditDetails - 1)] + "…"));
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
