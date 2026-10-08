using System.ComponentModel.DataAnnotations;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Releaser.Server.Infrastructure.Http;

public sealed class RateLimitOptions
{
    public const string Section = "RateLimits";

    /// <summary>Client (feed and notes) requests per remote IP per minute.</summary>
    [Range(1, 1_000_000)]
    public int ClientPerMinute { get; set; } = 600;

    /// <summary>Login attempts per remote IP per minute.</summary>
    [Range(1, 1000)]
    public int LoginPerMinute { get; set; } = 10;
}

internal static class RateLimiting
{
    public const string ClientPolicy = "client";
    public const string LoginPolicy = "login";

    public static IServiceCollection AddReleaserRateLimiting(this IServiceCollection services, RateLimitOptions options) =>
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(ClientPolicy, http => PerIp(http, options.ClientPerMinute));
            limiter.AddPolicy(LoginPolicy, http => PerIp(http, options.LoginPerMinute));
        });

    private static RateLimitPartition<string> PerIp(HttpContext http, int permitsPerMinute) =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
}
