using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Errors;

internal sealed partial class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Features.Get<ICorrelationIdFeature>()?.CorrelationId ?? httpContext.TraceIdentifier;
        var problem = Describe(exception);
        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(logger, exception, correlationId);
        }
        else
        {
            LogRejected(logger, correlationId, problem.Code, exception.Message);
        }

        httpContext.Response.StatusCode = problem.Status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new()
            {
                Status = problem.Status,
                Title = problem.Title,
                Instance = httpContext.Request.Path,
                Extensions = { [ResultExtensions.CodeExtension] = problem.Code },
            },
        });
    }

    private static Outcome Describe(Exception exception) =>
        exception switch
        {
            BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
                new(StatusCodes.Status413PayloadTooLarge, ProblemTypes.RequestTooLarge, null),
            BadHttpRequestException bad =>
                new(bad.StatusCode, ProblemTypes.RequestMalformed, "The request could not be read."),
            _ => new(StatusCodes.Status500InternalServerError, ProblemTypes.ServerError, "An unexpected error occurred."),
        };

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for request {CorrelationId}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string correlationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected request {CorrelationId} as {Code}: {Reason}")]
    private static partial void LogRejected(ILogger logger, string correlationId, string code, string reason);

    // A null title lets the framework fill in the status phrase, which is what the binder's own 413 carries too.
    private readonly record struct Outcome(int Status, string Code, string? Title);
}
