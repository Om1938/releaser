using FluentValidation;

namespace Releaser.Server.Infrastructure.Http;

/// <summary>Runs the FluentValidation validator for the request body of type <typeparamref name="T"/> before the handler.</summary>
internal sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<T>().FirstOrDefault();
        if (request is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Request body is required.");
        }
        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        return result.IsValid ? await next(context) : Results.ValidationProblem(result.ToDictionary());
    }
}

internal static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder) where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>().ProducesValidationProblem();
}
