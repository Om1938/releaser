using Releaser.Server.Infrastructure.Auth;

namespace Releaser.Server.Infrastructure.Http;

internal static class EndpointConventions
{
    /// <summary>Restricts a mutating endpoint to release managers.</summary>
    public static RouteHandlerBuilder RequiresReleaseManager(this RouteHandlerBuilder builder) =>
        builder.RequireAuthorization(AdminRoles.CanManageReleases);

    /// <summary>Adds the standard error responses of admin endpoints to OpenAPI.</summary>
    public static RouteGroupBuilder WithAdminProblems(this RouteGroupBuilder group) =>
        group
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
