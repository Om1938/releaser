using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Releaser.Server.Infrastructure.Manifests;

namespace Releaser.Server.Tests.Infrastructure;

public sealed record FactoryOptions(string ConnectionString, StubManifestHost Manifests, int FreshnessSeconds = 1, string? RedisConnectionString = null);

/// <summary>An in-memory instance of the platform (one "node") against a real PostgreSQL database.</summary>
public sealed class ReleaserFactory(FactoryOptions options) : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@example.test";
    public const string AdminPassword = "Correct-Horse-Battery-1";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Releaser", options.ConnectionString);
        builder.UseSetting("Resolution:FreshnessSeconds", options.FreshnessSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Bootstrap:AdminEmail", AdminEmail);
        builder.UseSetting("Bootstrap:AdminPassword", AdminPassword);
        builder.UseSetting("RateLimits:ClientPerMinute", "1000000");
        builder.UseSetting("RateLimits:LoginPerMinute", "1000");
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
        if (options.RedisConnectionString is not null)
        {
            builder.UseSetting("Redis:ConnectionString", options.RedisConnectionString);
        }
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IManifestFetcher>();
            services.AddSingleton<IManifestFetcher>(options.Manifests);
        });
    }

    public AdminClient CreateAdminClient() => new(CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, BaseAddress = new Uri("https://localhost") }));

    public FeedClient CreateFeedClient() => new(CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, BaseAddress = new Uri("https://localhost") }));
}
