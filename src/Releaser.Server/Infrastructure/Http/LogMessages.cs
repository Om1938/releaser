using Releaser.Domain.Resolution;
using Releaser.Domain.Targeting;

namespace Releaser.Server.Infrastructure.Http;

/// <summary>High-performance structured log messages.</summary>
internal static partial class LogMessages
{
    [LoggerMessage(1, LogLevel.Error, "Unhandled exception for {Method} {Path}")]
    public static partial void UnhandledException(this ILogger logger, Exception exception, string method, string path);

    [LoggerMessage(2, LogLevel.Warning, "Update resolution unavailable for {AppKey}; failing closed")]
    public static partial void ResolutionUnavailable(this ILogger logger, Exception exception, string appKey);

    [LoggerMessage(3, LogLevel.Debug, "Feed decision {Reason} for {AppKey} {Platform} {CurrentVersion}")]
    public static partial void FeedDecision(this ILogger logger, DecisionReason reason, string appKey, PlatformTarget platform, string currentVersion);

    [LoggerMessage(4, LogLevel.Warning, "No administrator exists. Set Bootstrap__AdminEmail and Bootstrap__AdminPassword to create the first one.")]
    public static partial void NoAdministrator(this ILogger logger);

    [LoggerMessage(5, LogLevel.Information, "Created bootstrap administrator {Email}")]
    public static partial void BootstrapAdministratorCreated(this ILogger logger, string email);
}
