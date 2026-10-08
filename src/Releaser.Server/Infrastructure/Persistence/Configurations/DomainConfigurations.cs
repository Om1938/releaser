using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Releaser.Domain.Applications;
using Releaser.Domain.Common;
using Releaser.Domain.Deployments;
using Releaser.Domain.Policies;
using Releaser.Domain.ReleaseNotes;
using Releaser.Domain.Releases;
using Releaser.Domain.Targeting;

namespace Releaser.Server.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.Property(a => a.Id).HasConversion(Conversions.AppId);
        builder.Property(a => a.Key).HasConversion(Conversions.ApplicationKey).HasMaxLength(64);
        builder.HasIndex(a => a.Key).IsUnique();
        builder.Property(a => a.Name).HasMaxLength(200);
        builder.Property(a => a.Description).HasMaxLength(2000);
        builder.Property(a => a.DefaultChannel).HasConversion(Conversions.ChannelKey).HasMaxLength(64);
        builder.Property(a => a.SupportedPlatforms).HasJsonConversion();
        builder.Property(a => a.ConfigVersion);
    }
}

internal sealed class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        builder.Property(c => c.AppId).HasConversion(Conversions.AppId);
        builder.Property(c => c.Key).HasConversion(Conversions.ChannelKey).HasMaxLength(64);
        builder.Property(c => c.Name).HasMaxLength(200);
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.HasIndex(c => new { c.AppId, c.Key }).IsUnique();
        builder.HasOne<Application>().WithMany().HasForeignKey(c => c.AppId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReleaseConfiguration : IEntityTypeConfiguration<Release>
{
    public void Configure(EntityTypeBuilder<Release> builder)
    {
        builder.Property(r => r.Id).HasConversion(Conversions.ReleaseId);
        builder.Property(r => r.AppId).HasConversion(Conversions.AppId);
        builder.Property(r => r.Version).HasConversion(Conversions.Version).HasMaxLength(128);
        builder.Property(r => r.Title).HasMaxLength(200);
        builder.Property(r => r.Channels).HasField("_channels").UsePropertyAccessMode(PropertyAccessMode.Field).HasJsonListConversion();
        builder.Property(r => r.Platforms).HasField("_platforms").UsePropertyAccessMode(PropertyAccessMode.Field).HasJsonListConversion();
        builder.HasIndex(r => new { r.AppId, r.Version }).IsUnique();
        builder.Property<uint>("RowVersion").IsRowVersion();
        builder.HasOne<Application>().WithMany().HasForeignKey(r => r.AppId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DeploymentConfiguration : IEntityTypeConfiguration<Deployment>
{
    public void Configure(EntityTypeBuilder<Deployment> builder)
    {
        builder.Property(d => d.Id).HasConversion(Conversions.DeploymentId);
        builder.Property(d => d.AppId).HasConversion(Conversions.AppId);
        builder.Property(d => d.ReleaseId).HasConversion(Conversions.ReleaseId);
        builder.Property(d => d.AudienceId).HasConversion(Conversions.AudienceId);
        builder.Property(d => d.Channel).HasConversion(Conversions.ChannelKey).HasMaxLength(64);
        builder.Property(d => d.Percentage).HasConversion(Conversions.Percentage).HasColumnName("percentage_basis_points");
        builder.Property(d => d.Name).HasMaxLength(200);
        builder.Property(d => d.CohortSalt).HasMaxLength(64);
        builder.HasIndex(d => new { d.AppId, d.State });
        builder.Property<uint>("RowVersion").IsRowVersion();
        builder.HasOne<Application>().WithMany().HasForeignKey(d => d.AppId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Release>().WithMany().HasForeignKey(d => d.ReleaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Audience>().WithMany().HasForeignKey(d => d.AudienceId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AudienceConfiguration : IEntityTypeConfiguration<Audience>
{
    public void Configure(EntityTypeBuilder<Audience> builder)
    {
        builder.Property(a => a.Id).HasConversion(Conversions.AudienceId);
        builder.Property(a => a.AppId).HasConversion(Conversions.AppId);
        builder.Property(a => a.Name).HasMaxLength(200);
        builder.Property(a => a.Description).HasMaxLength(2000);
        builder.Property(a => a.Includes).HasJsonConversion();
        builder.Property(a => a.Excludes).HasJsonConversion();
        builder.HasIndex(a => new { a.AppId, a.Name }).IsUnique();
        builder.HasOne<Application>().WithMany().HasForeignKey(a => a.AppId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.Property(p => p.Id).HasConversion(Conversions.PolicyId);
        builder.Property(p => p.AppId).HasConversion(Conversions.AppId);
        builder.Property(p => p.AudienceId).HasConversion(Conversions.AudienceId);
        builder.Property(p => p.ReleaseId).HasConversion(Conversions.ReleaseId);
        builder.Property(p => p.Name).HasMaxLength(200);
        builder.HasOne<Application>().WithMany().HasForeignKey(p => p.AppId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Audience>().WithMany().HasForeignKey(p => p.AudienceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Release>().WithMany().HasForeignKey(p => p.ReleaseId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReleaseNoteConfiguration : IEntityTypeConfiguration<ReleaseNote>
{
    public void Configure(EntityTypeBuilder<ReleaseNote> builder)
    {
        builder.Property(n => n.AppId).HasConversion(Conversions.AppId);
        builder.Property(n => n.ReleaseId).HasConversion(Conversions.ReleaseId);
        builder.Property(n => n.Content).HasJsonConversion();
        builder.Property(n => n.Revision).IsConcurrencyToken();
        builder.HasIndex(n => n.ReleaseId).IsUnique();
        builder.HasOne<Release>().WithMany().HasForeignKey(n => n.ReleaseId).OnDelete(DeleteBehavior.Restrict);
    }
}
