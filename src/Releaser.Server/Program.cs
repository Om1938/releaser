using Microsoft.Extensions.Options;
using Releaser.Server;
using Releaser.Server.Features.Admin;
using Releaser.Server.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.AddReleaser();
var app = builder.Build();

if (args.Contains("migrate"))
{
    await StartupTasks.MigrateAsync(app.Services, CancellationToken.None);
    Log.Information("Migrations applied");
    return;
}

if (app.Services.GetRequiredService<IOptions<PlatformOptions>>().Value.TrustForwardedHeaders)
{
    app.UseForwardedHeaders();
}
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapReleaser();

if (!BuildContext.IsGeneratingOpenApi)
{
    if (app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value.MigrateOnStartup)
    {
        await StartupTasks.MigrateAsync(app.Services, app.Lifetime.ApplicationStopping);
    }
    await StartupTasks.BootstrapAsync(app.Services, app.Services.GetRequiredService<IOptions<BootstrapOptions>>().Value, app.Logger);
}

await app.RunAsync();

/// <summary>Entry point; public for integration tests.</summary>
public partial class Program;
