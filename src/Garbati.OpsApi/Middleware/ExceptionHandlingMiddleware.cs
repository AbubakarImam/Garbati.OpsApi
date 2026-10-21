using Garbati.OpsApi.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Garbati.OpsApi.Middleware;

// Same shape as every sibling service's middleware of the same name (ARCHITECTURE.md Section 5's
// status-code mapping: 404 not found, 409 conflict, 422 business-rule failure, 500 unexpected; 400/401/403
// are handled per-controller/by the session-cookie authentication handler, not here).
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request cancelled by client: {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, title) = Map(ex);
            var isExpected = status != StatusCodes.Status500InternalServerError;

            if (isExpected)
                logger.LogWarning(ex, "Request rejected ({Status}) {Method} {Path}", status, context.Request.Method, context.Request.Path);
            else
                logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.StatusCode = status;

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                // Only expected business errors expose their message; unexpected ones must not leak internals.
                Detail = isExpected ? ex.Message : null,
                Extensions = { ["correlationId"] = context.TraceIdentifier }
            };

            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
        }
    }

    private static (int Status, string Title) Map(Exception ex) => ex switch
    {
        // T-034: the two maker-checker workflows' own domain-rule exceptions (not a downstream call -
        // raised by this service's own ReversalRequestService/CustomerEditRequestService).
        OpsRequestNotFoundException => (StatusCodes.Status404NotFound, "The request was not found."),
        OpsRequestNotPendingException => (StatusCodes.Status409Conflict, "This request is no longer pending."),
        MakerCheckerViolationException => (StatusCodes.Status422UnprocessableEntity, "The maker-checker rule was violated."),
        DownstreamApiException downstream when downstream.StatusCode == StatusCodes.Status404NotFound
            => (StatusCodes.Status404NotFound, $"{downstream.ServiceName} reported not found."),
        DownstreamApiException downstream when downstream.StatusCode == StatusCodes.Status409Conflict
            => (StatusCodes.Status409Conflict, $"{downstream.ServiceName} reported a conflict."),
        DownstreamApiException downstream when downstream.StatusCode == StatusCodes.Status422UnprocessableEntity
            => (StatusCodes.Status422UnprocessableEntity, $"{downstream.ServiceName} rejected the request."),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
    };
}
