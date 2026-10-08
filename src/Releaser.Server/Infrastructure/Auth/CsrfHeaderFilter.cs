namespace Releaser.Server.Infrastructure.Auth;

/// <summary>
/// Requires a custom header on state-changing admin requests. Cross-site forms cannot set it, complementing SameSite=Strict cookies (ADR 0009).
/// </summary>
internal sealed class CsrfHeaderFilter : IEndpointFilter
{
    public const string HeaderName = "X-Releaser-Csrf";

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        var isSafe = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method);
        if (isSafe || request.Headers.ContainsKey(HeaderName))
        {
            return next(context);
        }
        return ValueTask.FromResult<object?>(Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Missing CSRF header",
            detail: $"State-changing requests must include the '{HeaderName}' header."));
    }
}
