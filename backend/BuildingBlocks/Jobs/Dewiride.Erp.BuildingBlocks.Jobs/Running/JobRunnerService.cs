using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Running;

// Polls only once the application has started, so a host whose start fails claims nothing, and stops claiming as soon as the
// shutdown begins, which the host announces before it stops any hosted service. A running job keeps its token until the host's
// shutdown timeout ends, so a short job finishes and records its outcome; then every job's token fires, and the attempts get a
// last few seconds to put their runs back as pending before the process goes. The API container's stop grace period has to
// outlast the host's timeout, or Docker kills the process first and the runs wait for their leases to expire.
internal sealed partial class JobRunnerService(
    JobRunner runner,
    JobScheduler scheduler,
    JobSignal signal,
    IHostApplicationLifetime lifetime,
    IOptions<JobRunnerOptions> options,
    TimeProvider time,
    ILogger<JobRunnerService> logger) : BackgroundService
{
    public static readonly TimeSpan RecordingGrace = TimeSpan.FromSeconds(5);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        var idle = runner.WhenIdleAsync();
        try
        {
            await idle.WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }

        runner.Abort();
        try
        {
            await idle.WaitAsync(RecordingGrace, time, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            LogStillRunning(runner.ActiveAttempts);
            runner.Abandon();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var claims = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, lifetime.ApplicationStopping);
        var token = claims.Token;
        try
        {
            await WaitForApplicationStartAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }

        var failures = 0;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await scheduler.EnqueueDueAsync(token).ConfigureAwait(false);
                await runner.RecoverExpiredLeasesAsync(token).ConfigureAwait(false);
                await runner.StartDueAsync(token).ConfigureAwait(false);
                if (failures > 0)
                {
                    LogPollRecovered(failures);
                    failures = 0;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                failures++;
                if (failures == 1)
                {
                    LogPollFailed(exception);
                }
                else
                {
                    LogPollStillFailing(exception, failures);
                }
            }

            try
            {
                await signal.WaitAsync(options.Value.PollInterval, time, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task WaitForApplicationStartAsync(CancellationToken cancellationToken)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        await started.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Polling for due jobs failed; the runner polls again at its next interval")]
    private partial void LogPollFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Polling for due jobs failed again, {Failures} times in a row")]
    private partial void LogPollStillFailing(Exception exception, int failures);

    [LoggerMessage(Level = LogLevel.Information, Message = "Polling for due jobs works again after {Failures} failed polls")]
    private partial void LogPollRecovered(int failures);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Attempts} job attempts were still running when the shutdown ended; their runs are recovered once their leases expire")]
    private partial void LogStillRunning(int attempts);
}
