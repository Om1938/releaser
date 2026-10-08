using Microsoft.AspNetCore.Http.HttpResults;
using Releaser.Domain.Common;
using Releaser.Domain.Resolution;
using Releaser.Domain.Targeting;
using Releaser.Electron;
using Releaser.Server.Infrastructure.Auth;
using Releaser.Server.Infrastructure.Caching;
using Releaser.Server.Infrastructure.Http;

namespace Releaser.Server.Features.Feed;

/// <summary>
/// electron-updater generic-provider feed (ADR 0002). Applications set
/// <c>https://host/u/{appKey}/{installationId}/{currentVersion}/</c> as feed URL; the updater appends <c>{channel}{suffix}.yml</c>.
/// </summary>
internal static class UpdateFeedEndpoint
{
    public const string ContentType = "text/yaml; charset=utf-8";

    public static IEndpointRouteBuilder MapUpdateFeed(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/u/{appKey}/{installationId}/{currentVersion}/{file}", GetFeedAsync)
            .WithName("GetUpdateFeed")
            .WithTags("Client: update feed")
            .WithSummary("electron-updater metadata for one installation")
            .WithDescription("Returns the electron-builder manifest of the offered release with absolute external download URLs, or a manifest announcing the current version (no update). Never serves binaries.")
            .Produces<string>(StatusCodes.Status200OK, ContentType)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable)
            .RequireRateLimiting(RateLimiting.ClientPolicy)
            .AllowAnonymous();
        return routes;
    }

    private static async Task<IResult> GetFeedAsync(
        string appKey, string installationId, string currentVersion, string file,
        HttpContext http, FeedSnapshotCache snapshots, FeedManifestRenderer renderer, ContextTokenValidator tokens,
        ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!TryParseRequest(appKey, installationId, currentVersion, file, out var request))
        {
            return TypedResults.BadRequest();
        }
        var logger = loggers.CreateLogger(typeof(UpdateFeedEndpoint));
        FeedSnapshot? snapshot;
        try
        {
            snapshot = await snapshots.GetAsync(request.AppKey, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Fail closed (ADR 0008): electron-updater treats 503 as an error and does not update.
            logger.ResolutionUnavailable(exception, appKey);
            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        var channel = snapshot is null ? null : request.File.ResolveChannel(snapshot.Resolution.DefaultChannel);
        if (snapshot is null || channel is null)
        {
            return TypedResults.NotFound();
        }
        var identity = await tokens.ValidateAsync(http.Request.Headers.Authorization, snapshot, request.Installation);
        var context = new TargetContext(channel, request.File.Platform, request.CurrentVersion, request.Installation, identity.Identity);
        var decision = UpdateResolver.Resolve(snapshot.Resolution, context);
        logger.FeedDecision(decision.Reason, appKey, request.File.Platform, currentVersion);

        var body = decision.IsUpdateOffered
            ? await renderer.RenderAsync(snapshot, decision.Release!.Id, request.File.Platform, cancellationToken)
            : ElectronManifest.NoUpdate(request.CurrentVersion);
        return TypedResults.Text(body, ContentType);
    }

    private static bool TryParseRequest(string appKey, string installationId, string currentVersion, string file, out FeedRequest request)
    {
        request = null!;
        if (!SemanticVersion.TryParse(currentVersion, out var version)
            || !InstallationId.TryFrom(installationId, out var installation)
            || !FeedFileName.TryParse(file, out var feedFile))
        {
            return false;
        }
        try
        {
            request = new FeedRequest(ApplicationKey.From(appKey), installation!, version, feedFile!);
            return true;
        }
        catch (DomainRuleException)
        {
            return false;
        }
    }

    private sealed record FeedRequest(ApplicationKey AppKey, InstallationId Installation, SemanticVersion CurrentVersion, FeedFileName File);
}
