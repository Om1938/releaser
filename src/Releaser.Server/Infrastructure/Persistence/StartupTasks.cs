using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Releaser.Server.Features.Audit;
using Releaser.Server.Infrastructure.Auth;
using Releaser.Server.Infrastructure.Http;

namespace Releaser.Server.Infrastructure.Persistence;

public sealed class BootstrapOptions
{
    public const string Section = "Bootstrap";

    /// <summary>Email of the first administrator; used only while no administrator accounts exist.</summary>
    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }
}

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary>Apply pending EF Core migrations at startup. Disable to run <c>Releaser.Server migrate</c> explicitly.</summary>
    public bool MigrateOnStartup { get; set; } = true;
}

/// <summary>Explicit migrations and first-administrator bootstrap.</summary>
internal static class StartupTasks
{
    /// <summary>Arbitrary constant identifying the migration advisory lock.</summary>
    private const long MigrationLockKey = 0x52656c6561736572; // "Releaser"

    /// <summary>
    /// Applies pending migrations while holding a PostgreSQL session advisory lock, so several instances starting together
    /// (scale-out profile) migrate one at a time instead of racing.
    /// </summary>
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReleaserDbContext>();
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_lock({MigrationLockKey})", cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({MigrationLockKey})", CancellationToken.None);
            await db.Database.CloseConnectionAsync();
        }
    }

    public static async Task BootstrapAsync(IServiceProvider services, BootstrapOptions options, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in AdminRoles.All.Where(role => !roles.Roles.Any(r => r.Name == role)))
        {
            await roles.CreateAsync(new IdentityRole<Guid>(role));
        }
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>();
        if (await users.Users.AnyAsync())
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(options.AdminEmail) || string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            logger.NoAdministrator();
            return;
        }
        var admin = new AdminUser { UserName = options.AdminEmail, Email = options.AdminEmail, DisplayName = "Administrator", EmailConfirmed = true };
        var result = await users.CreateAsync(admin, options.AdminPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Bootstrap administrator could not be created: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
        await users.AddToRoleAsync(admin, AdminRoles.Admin);
        var db = scope.ServiceProvider.GetRequiredService<ReleaserDbContext>();
        db.AuditEntries.Add(AuditEntry.Create(AuditActor.System, new AuditRecord("user.bootstrapped", "user", admin.Id.ToString(), Details: admin.Email), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        logger.BootstrapAdministratorCreated(options.AdminEmail);
    }
}
