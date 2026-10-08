using System.Reflection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Releaser.Server.Infrastructure.Caching;

namespace Releaser.Server.Features.Admin;

public sealed record SystemInfoResponse(string Version, int FreshnessSeconds, bool DistributedCacheEnabled, string PublicBaseUrl);

public sealed class PlatformOptions
{
    public const string Section = "Platform";

    /// <summary>Public base URL of this instance, used to show feed URLs in the dashboard.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>Trust X-Forwarded-For/Proto. Enable only when a reverse proxy you control is the sole way in.</summary>
    public bool TrustForwardedHeaders { get; set; }
}

internal static class SystemEndpoints
{
    public static RouteGroupBuilder MapSystem(this RouteGroupBuilder admin)
    {
        admin.MapGet("/system", GetInfo).WithName("GetSystemInfo").WithTags("System");
        return admin;
    }

    private static Ok<SystemInfoResponse> GetInfo(IOptions<ResolutionOptions> resolution, IOptions<PlatformOptions> platform, IConfiguration configuration) =>
        TypedResults.Ok(new SystemInfoResponse(
            typeof(SystemEndpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev",
            resolution.Value.FreshnessSeconds,
            !string.IsNullOrWhiteSpace(configuration["Redis:ConnectionString"]),
            platform.Value.PublicBaseUrl.TrimEnd('/')));
}
