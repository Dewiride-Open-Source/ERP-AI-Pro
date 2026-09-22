using System.Diagnostics;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.Application.Pipeline.Steps;

internal sealed partial class LoggingStep(ILogger<LoggingStep> logger) : IPipelineStep
{
    public const string ActivitySourceName = "Dewiride.Erp.Application";

    private static readonly ActivitySource Source = new(ActivitySourceName);

    public PipelineStage Stage => PipelineStage.Logging;

    public async Task<Result<TResult>> InvokeAsync<TRequest, TResult>(TRequest request, HandlerDescriptor descriptor, PipelineContinuation<TResult> next, CancellationToken cancellationToken)
        where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(next);

        using var activity = Source.StartActivity(descriptor.RequestType.Name);
        activity?.SetTag("erp.request", descriptor.RequestType.FullName);
        activity?.SetTag("erp.handler", descriptor.HandlerType.FullName);
        activity?.SetTag("erp.handler.kind", descriptor.Kind.ToString());
        var level = descriptor.Kind == HandlerKind.Command ? LogLevel.Information : LogLevel.Debug;
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await next(cancellationToken).ConfigureAwait(false);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (result.IsFailure)
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.Error!.Code);
                activity?.SetTag("erp.error.code", result.Error!.Code);
                LogRejected(logger, level, descriptor.RequestType.Name, result.Error!.Code, elapsed);
            }
            else
            {
                LogHandled(logger, level, descriptor.RequestType.Name, elapsed);
            }

            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            LogThrew(logger, descriptor.RequestType.Name, exception.GetType().Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }

    [LoggerMessage(Message = "Handled {Request} in {ElapsedMs:0.0} ms")]
    private static partial void LogHandled(ILogger logger, LogLevel level, string request, double elapsedMs);

    [LoggerMessage(Message = "Rejected {Request} with {ErrorCode} in {ElapsedMs:0.0} ms")]
    private static partial void LogRejected(ILogger logger, LogLevel level, string request, string errorCode, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Request} threw {ExceptionType} after {ElapsedMs:0.0} ms")]
    private static partial void LogThrew(ILogger logger, string request, string exceptionType, double elapsedMs);
}
