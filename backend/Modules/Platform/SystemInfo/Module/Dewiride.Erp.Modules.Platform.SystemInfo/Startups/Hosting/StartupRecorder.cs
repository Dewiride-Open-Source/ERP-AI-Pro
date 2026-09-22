using System.Runtime.InteropServices;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Application.Commands.RecordStartup;
using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Hosting;

internal sealed partial class StartupRecorder(
    IHostApplicationLifetime lifetime,
    IServiceScopeFactory scopeFactory,
    ApplicationInfo application,
    IHostEnvironment environment,
    ErpConfigurationInfo configuration,
    ILogger<StartupRecorder> logger) : BackgroundService
{
    private static readonly Error Skipped = Error.Failure("startup.module-disabled", "The system-info module is disabled, so the start was not recorded.");

    private readonly TaskCompletionSource<Result<ApiStartupId>> _recorded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<Result<ApiStartupId>> Recorded => _recorded.Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await WaitForApplicationStartAsync(stoppingToken);
            _recorded.TrySetResult(await RecordAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _recorded.TrySetCanceled(stoppingToken);
        }
        catch (Exception exception)
        {
            LogFailed(exception);
            _recorded.TrySetResult(Error.Failure("startup.record-failed", exception.Message));
        }
    }

    private async Task<Result<ApiStartupId>> RecordAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var features = scope.ServiceProvider.GetRequiredService<IFeatureManager>();
        if (!await features.IsEnabledAsync(SystemInfoModule.FeatureFlag, cancellationToken))
        {
            LogSkipped();
            return Skipped;
        }

        var command = new RecordStartupCommand(
            application.Name,
            new BuildInfo(application.Version, RuntimeInformation.FrameworkDescription),
            environment.EnvironmentName,
            configuration.Label,
            Environment.MachineName,
            application.StartedAt);
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<RecordStartupCommand, ApiStartupId>>();
        var result = await handler.HandleAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            LogRejected(result.Error!.Code, result.Error.Message);
        }
        else
        {
            LogRecorded(result.Value.Value);
        }

        return result;
    }

    private async Task WaitForApplicationStartAsync(CancellationToken cancellationToken)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        await started.Task.WaitAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Recorded this API start as {StartupId}.")]
    private partial void LogRecorded(Guid startupId);

    [LoggerMessage(Level = LogLevel.Information, Message = "The system-info module is disabled; this API start was not recorded.")]
    private partial void LogSkipped();

    [LoggerMessage(Level = LogLevel.Warning, Message = "This API start was not recorded: {Code} {Message}")]
    private partial void LogRejected(string code, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "Recording this API start failed.")]
    private partial void LogFailed(Exception exception);
}
