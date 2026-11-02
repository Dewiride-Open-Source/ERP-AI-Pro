using Dewiride.Erp.BuildingBlocks.Jobs.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Running.Attempts;

// Keeps an attempt's lease alive while its job runs, in a scope of its own, and reads at each renewal whether an administrator
// asked the run to stop. A lease found taken over, or a stop request, cancels the job's token; after a stop request the lease
// is still renewed, so the run is not recovered while its job winds down. A renewal that fails is retried at the next interval.
// Start sets the first renewal's timer before it returns, so the interval counts from the start of the job whatever the thread
// pool is doing.
internal sealed partial class JobLease : IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopes;

    private readonly ClaimedRun _run;

    private readonly TimeSpan _duration;

    private readonly TimeSpan _interval;

    private readonly TimeProvider _time;

    private readonly ILogger _logger;

    private readonly CancellationTokenSource _stop = new();

    private readonly CancellationTokenSource _ended = new();

    private Task _renewals = Task.CompletedTask;

    private volatile bool _lost;

    private volatile bool _cancelRequested;

    public JobLease(IServiceScopeFactory scopes, ClaimedRun run, JobRunnerOptions options, TimeProvider time, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _scopes = scopes;
        _run = run;
        _duration = options.LeaseDuration;
        _interval = options.RenewalInterval;
        _time = time;
        _logger = logger;
    }

    public CancellationToken Token => _ended.Token;

    public bool Lost => _lost;

    public bool CancelRequested => _cancelRequested;

    public void Start() => _renewals = RenewAsync();

    public async Task StopAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        await _renewals.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _stop.Dispose();
        _ended.Dispose();
    }

    private async Task RenewAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(_interval, _time, _stop.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
                var state = await JobRunTransitions.RenewAsync(context, _run.Id, _run.LeaseToken, _time.GetUtcNow() + _duration, _stop.Token).ConfigureAwait(false);
                if (state == LeaseState.Lost)
                {
                    _lost = true;
                    LogLeaseLost(_logger, _run.JobName, _run.Attempt);
                    await _ended.CancelAsync().ConfigureAwait(false);
                    return;
                }

                if (state == LeaseState.CancelRequested && !_cancelRequested)
                {
                    _cancelRequested = true;
                    LogStopRequested(_logger, _run.JobName, _run.Attempt);
                    await _ended.CancelAsync().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogRenewalFailed(_logger, exception, _run.JobName, _run.Attempt);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Attempt {Attempt} of {JobName} lost its lease to another runner; its job was told to stop and its outcome is discarded")]
    private static partial void LogLeaseLost(ILogger logger, string jobName, int attempt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Attempt {Attempt} of {JobName} was asked to stop; its job was told to stop")]
    private static partial void LogStopRequested(ILogger logger, string jobName, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Renewing the lease of attempt {Attempt} of {JobName} failed; the next renewal tries again")]
    private static partial void LogRenewalFailed(ILogger logger, Exception exception, string jobName, int attempt);
}
