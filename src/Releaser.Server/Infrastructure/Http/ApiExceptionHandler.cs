using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Releaser.Domain.Common;
using Releaser.Server.Infrastructure.Manifests;

namespace Releaser.Server.Infrastructure.Http;

/// <summary>Maps expected failures to RFC 9457 problem details; unexpected ones become an opaque 500.</summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            DomainRuleException rule => Problem(StatusCodes.Status409Conflict, "Business rule violated", rule.Message, rule.Code),
            ManifestFetchException fetch => Problem(StatusCodes.Status422UnprocessableEntity, "Manifest could not be fetched", fetch.Message, "manifest.fetch_failed"),
            DbUpdateConcurrencyException => Problem(StatusCodes.Status409Conflict, "Concurrent modification", "The entity was changed by someone else. Reload and retry.", "concurrency"),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                Problem(StatusCodes.Status409Conflict, "Already exists", "An entity with the same unique key already exists.", "duplicate"),
            BadHttpRequestException bad => Problem(bad.StatusCode, "Bad request", bad.Message, "bad_request"),
            _ => null,
        };
        if (problem is null)
        {
            logger.UnhandledException(exception, httpContext.Request.Method, httpContext.Request.Path);
            problem = Problem(StatusCodes.Status500InternalServerError, "Unexpected error", "An unexpected error occurred.", "unexpected");
        }
        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem, Exception = exception });
    }

    private static ProblemDetails Problem(int status, string title, string detail, string code) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Extensions = { ["code"] = code },
    };
}
