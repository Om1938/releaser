using System.Data;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Releaser.Domain.Applications;
using Releaser.Domain.Common;
using Releaser.Domain.Deployments;
using Releaser.Domain.Policies;
using Releaser.Domain.ReleaseNotes;
using Releaser.Domain.Releases;
using Releaser.Domain.Targeting;
using Releaser.Server.Features.Audit;
using Releaser.Server.Features.ContextKeys;
using Releaser.Server.Features.ReleaseNotes;
using Releaser.Server.Features.Releases;
using Releaser.Server.Infrastructure.Caching;

namespace Releaser.Server.Infrastructure.Persistence;

/// <summary>
/// Authoritative store. Any change to application-scoped data atomically increments the owning application's
/// <see cref="Application.ConfigVersion"/> in the same transaction (ADR 0008).
/// </summary>
public sealed class ReleaserDbContext(DbContextOptions<ReleaserDbContext> options, IConfigVersionListener? listener = null)
    : IdentityDbContext<AdminUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<Release> Releases => Set<Release>();
    public DbSet<ReleaseManifest> ReleaseManifests => Set<ReleaseManifest>();
    public DbSet<Deployment> Deployments => Set<Deployment>();
    public DbSet<Audience> Audiences => Set<Audience>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<ReleaseNote> ReleaseNotes => Set<ReleaseNote>();
    public DbSet<ReleaseNoteRevision> ReleaseNoteRevisions => Set<ReleaseNoteRevision>();
    public DbSet<ContextSigningKey> ContextSigningKeys => Set<ContextSigningKey>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>ASP.NET Core Data Protection keys, shared by all instances so admin cookies survive restarts and scale-out.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ReleaserDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var changedApps = ChangedApplications();
        if (changedApps.Count == 0)
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        var ownsTransaction = Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken) : null;
        try
        {
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            await Applications
                .Where(application => changedApps.Contains(application.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.ConfigVersion, a => a.ConfigVersion + 1), cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            NotifyChanged(changedApps);
            return result;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Synchronous saves are allowed only for data that does not affect resolution (e.g. ASP.NET Core Data Protection keys);
    /// application-scoped changes must go through <see cref="SaveChangesAsync(bool, CancellationToken)"/> to bump versions atomically.
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        ChangedApplications().Count == 0
            ? base.SaveChanges(acceptAllChangesOnSuccess)
            : throw new NotSupportedException("Use SaveChangesAsync so configuration versions are bumped atomically.");

    private List<AppId> ChangedApplications()
    {
        var scoped = ChangeTracker.Entries<IApplicationScoped>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => entry.Entity.AppId);
        var applications = ChangeTracker.Entries<Application>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity.Id);
        return [.. scoped.Concat(applications).Distinct()];
    }

    private void NotifyChanged(List<AppId> applications)
    {
        foreach (var application in applications)
        {
            listener?.ConfigVersionChanged(application);
        }
    }
}
