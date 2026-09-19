using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Errors;

internal sealed partial class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        LogUnhandled(logger, exception, httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested
            ? StatusCodes.Status499ClientClosedRequest
            : StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new()
            {
                Status = httpContext.Response.StatusCode,
                Title = "An unexpected error occurred.",
                Instance = httpContext.Request.Path,
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for request {TraceId}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string traceId);
}
