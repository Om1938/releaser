using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Releaser.Server.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef migrations add</c> build the model without starting the application or a database.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ReleaserDbContext>
{
    public ReleaserDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ReleaserDbContext>()
            .UseNpgsql("Host=localhost;Database=releaser_design", npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention()
            .Options);
}
