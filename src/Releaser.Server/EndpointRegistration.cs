using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Releaser.Server.Features.Admin;
using Releaser.Server.Features.Applications;
using Releaser.Server.Features.Audiences;
using Releaser.Server.Features.Audit;
using Releaser.Server.Features.Channels;
using Releaser.Server.Features.ContextKeys;
using Releaser.Server.Features.Decisions;
using Releaser.Server.Features.Deployments;
using Releaser.Server.Features.Feed;
using Releaser.Server.Features.Policies;
using Releaser.Server.Features.ReleaseNotes;
using Releaser.Server.Features.Releases;
using Releaser.Server.Infrastructure.Auth;
using Releaser.Server.Infrastructure.Http;
using Scalar.AspNetCore;

namespace Releaser.Server;

internal static class EndpointRegistration
{
    public static WebApplication MapReleaser(this WebApplication app)
    {
        app.MapOpenApi("/openapi/{documentName}.json");
        app.MapScalarApiReference("/api/docs");

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

        // Update-facing surface: anonymous, rate-limited, no cookies, never exposes policy internals.
        app.MapUpdateFeed();
        app.MapGroup("/api/client/v1").RequireRateLimiting(RateLimiting.ClientPolicy).AllowAnonymous().MapClientReleaseNotes();

        // Administrative surface: cookie-authenticated publisher staff only.
        app.MapGroup("/api/admin/v1")
            .RequireAuthorization(AdminRoles.CanView)
            .AddEndpointFilter<CsrfHeaderFilter>()
            .WithAdminProblems()
            .MapAuth()
            .MapUsers()
            .MapSystem()
            .MapApplications()
            .MapChannels()
            .MapReleases()
            .MapReleaseNotes()
            .MapAudiences()
            .MapDeployments()
            .MapPolicies()
            .MapContextKeys()
            .MapDecisionExplainer()
            .MapAudit();

        app.MapFallback("/api/{**path}", () => Results.NotFound()).ExcludeFromDescription();
        app.MapFallback("/u/{**path}", () => Results.NotFound()).ExcludeFromDescription();
        app.MapFallbackToFile("index.html");
        return app;
    }
}
