using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Releaser.Domain.Applications;
using Releaser.Domain.ReleaseNotes;
using Releaser.Domain.Releases;
using Releaser.Server.Features.Audit;
using Releaser.Server.Features.ContextKeys;
using Releaser.Server.Features.ReleaseNotes;
using Releaser.Server.Features.Releases;

namespace Releaser.Server.Infrastructure.Persistence.Configurations;

internal sealed class ReleaseManifestConfiguration : IEntityTypeConfiguration<ReleaseManifest>
{
    public void Configure(EntityTypeBuilder<ReleaseManifest> builder)
    {
        builder.Property(m => m.AppId).HasConversion(Conversions.AppId);
        builder.Property(m => m.ReleaseId).HasConversion(Conversions.ReleaseId);
        builder.Property(m => m.SourceUrl).HasMaxLength(2048);
        builder.Property(m => m.SourceSha256).HasMaxLength(64);
        builder.HasIndex(m => new { m.ReleaseId, m.Platform }).IsUnique();
        builder.HasOne<Release>().WithMany().HasForeignKey(m => m.ReleaseId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReleaseNoteRevisionConfiguration : IEntityTypeConfiguration<ReleaseNoteRevision>
{
    public void Configure(EntityTypeBuilder<ReleaseNoteRevision> builder)
    {
        builder.Property(r => r.Content).HasJsonConversion();
        builder.Property(r => r.Actor).HasMaxLength(256);
        builder.HasIndex(r => new { r.ReleaseNoteId, r.Revision }).IsUnique();
        builder.HasOne<ReleaseNote>().WithMany().HasForeignKey(r => r.ReleaseNoteId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ContextSigningKeyConfiguration : IEntityTypeConfiguration<ContextSigningKey>
{
    public void Configure(EntityTypeBuilder<ContextSigningKey> builder)
    {
        builder.Property(k => k.AppId).HasConversion(Conversions.AppId);
        builder.Property(k => k.Name).HasMaxLength(200);
        builder.Property(k => k.PublicKeyPem).HasMaxLength(4096);
        builder.HasOne<Application>().WithMany().HasForeignKey(k => k.AppId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.Property(a => a.Id).UseIdentityAlwaysColumn();
        builder.Property(a => a.Actor).HasMaxLength(256);
        builder.Property(a => a.Action).HasMaxLength(100);
        builder.Property(a => a.EntityType).HasMaxLength(100);
        builder.Property(a => a.EntityId).HasMaxLength(100);
        builder.Property(a => a.Details).HasMaxLength(4000);
        builder.HasIndex(a => a.OccurredAt);
        builder.HasIndex(a => new { a.AppId, a.OccurredAt });
    }
}
