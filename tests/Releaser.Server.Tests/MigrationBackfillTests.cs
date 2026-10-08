using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Releaser.Server.Infrastructure.Persistence;
using Releaser.Server.Tests.Infrastructure;

namespace Releaser.Server.Tests;

/// <summary>Migrations that transform existing data are tested against data written in the previous schema.</summary>
public sealed class MigrationBackfillTests(PostgresContainer postgres)
{
    [Fact]
    public async Task supported_platforms_are_backfilled_from_existing_releases()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var db = new ReleaserDbContext(new DbContextOptionsBuilder<ReleaserDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention()
            .Options);
        var migrator = db.GetService<IMigrator>();
        var migrations = db.Database.GetMigrations().ToList();
        var target = migrations.Single(m => m.EndsWith("_SupportedPlatforms", StringComparison.Ordinal));
        await migrator.MigrateAsync(migrations[migrations.IndexOf(target) - 1]);

        Guid withReleases = Guid.NewGuid(), withoutReleases = Guid.NewGuid();
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var insert = new NpgsqlCommand($"""
                INSERT INTO applications (id, key, name, default_channel, config_version, created_at)
                VALUES ('{withReleases}', 'win-mac', 'Win+Mac', 'stable', 1, now()), ('{withoutReleases}', 'empty', 'Empty', 'stable', 1, now());
                INSERT INTO releases (id, app_id, version, state, registered_at, channels, platforms)
                VALUES (gen_random_uuid(), '{withReleases}', '1.0.0', 1, now(), '[]', '[1]'),
                       (gen_random_uuid(), '{withReleases}', '1.1.0', 3, now(), '[]', '[2,1]');
                """, connection);
            await insert.ExecuteNonQueryAsync();
        }

        await migrator.MigrateAsync();

        var platforms = await db.Applications.AsNoTracking().ToDictionaryAsync(a => a.Id.Value, a => a.SupportedPlatforms);
        platforms[withReleases].ShouldBe([Domain.Targeting.PlatformTarget.Windows, Domain.Targeting.PlatformTarget.MacOS]);
        platforms[withoutReleases].Count.ShouldBe(Enum.GetValues<Domain.Targeting.PlatformTarget>().Length);
    }
}
