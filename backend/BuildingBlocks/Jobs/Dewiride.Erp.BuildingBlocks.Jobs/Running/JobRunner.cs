using System.Collections.Concurrent;
using System.Diagnostics;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Persistence;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.BuildingBlocks.Jobs.Running.Attempts;
using Dewiride.Erp.BuildingBlocks.Jobs.Telemetry;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Running;

// Claims due runs of the jobs this release defines, up to the free slots, and runs each attempt on a task of its own, outside
// any request, so every attempt's scope answers the system as its actor. A job's token fires when its attempt overruns its
// limit, when an administrator asks it to stop, when its lease is lost, or when the host's shutdown timeout ends, never when
// shutdown merely begins. An attempt's outcome is recorded with no token until the shutdown's last grace has passed, and what
// the attempt logs and measures follows the ending that was written, which a stored stop request turns from a failure or an
// interruption into a cancel; an attempt whose ending could not be written is measured as unrecorded. Delivery is at least
// once: a process that dies after a job's work but before its outcome leaves the run to the runner that recovers its expired
// lease.
internal sealed partial class JobRunner : IDisposable
{
    public const int RecoveryBatchSize = 100;

    private const string LeaseLostOutcome = "lease-lost";

    private const string UnrecordedOutcome = "unrecorded";

    private readonly IServiceScopeFactory _scopes;

    private readonly JobCatalog _catalog;

    private readonly JobInvoker _invoker;

    private readonly JobSignal _signal;

    private readonly JobsMetrics _metrics;

    private readonly JobRunnerInstance _instance;

    private readonly IOptions<JobRunnerOptions> _options;

    private readonly TimeProvider _time;

    private readonly ILogger<JobRunner> _logger;

    private readonly CancellationTokenSource _abort = new();

    private readonly CancellationTokenSource _abandon = new();

    private readonly ConcurrentDictionary<long, Task> _attempts = new();

    private long _sequence;

    private int _slotsInUse;

    public JobRunner(
        IServiceScopeFactory scopes,
        JobCatalog catalog,
        JobInvoker invoker,
        JobSignal signal,
        JobsMetrics metrics,
        JobRunnerInstance instance,
        IOptions<JobRunnerOptions> options,
        TimeProvider time,
        ILogger<JobRunner> logger)
    {
        _scopes = scopes;
        _catalog = catalog;
        _invoker = invoker;
        _signal = signal;
        _metrics = metrics;
        _instance = instance;
        _options = options;
        _time = time;
        _logger = logger;
    }

    public int ActiveAttempts => Volatile.Read(ref _slotsInUse);

    public async Task<int> StartDueAsync(CancellationToken claimToken)
    {
        var free = _options.Value.MaxConcurrentRuns - ActiveAttempts;
        if (free <= 0 || _catalog.Names.Count == 0)
        {
            return 0;
        }

        List<JobRun> due;
        await using (var scope = _scopes.CreateAsyncScope())
        {
            due = await JobRunTransitions.DueAsync(Context(scope), _catalog.Names, _time.GetUtcNow(), free, claimToken).ConfigureAwait(false);
        }

        foreach (var run in due)
        {
            Start(run.JobName, () => ClaimAndRunAsync(run.Id, run.AttemptCount, run.JobName, claimToken, CancellationToken.None), claimToken);
        }

        return due.Count;
    }

    public async Task<CompletedAttempt?> TryRunAsync(JobRunId runId, CancellationToken cancellationToken)
    {
        JobRun? run;
        await using (var scope = _scopes.CreateAsyncScope())
        {
            run = await Context(scope).Runs.AsNoTracking().SingleOrDefaultAsync(r => r.Id == runId, cancellationToken).ConfigureAwait(false);
        }

        if (run is null || _catalog.Find(run.JobName) is null)
        {
            return null;
        }

        return await ClaimAndRunAsync(run.Id, run.AttemptCount, run.JobName, cancellationToken, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClaimedRun?> TryClaimAsync(JobRunId runId, int observedAttempts, string traceId, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var claim = new LeaseClaim(Guid.CreateVersion7(), _instance.Name, now, now + _options.Value.LeaseDuration, traceId);
        await using var scope = _scopes.CreateAsyncScope();

        return await JobRunTransitions.TryClaimAsync(Context(scope), runId, observedAttempts, claim, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> RecoverExpiredLeasesAsync(CancellationToken cancellationToken)
    {
        if (_catalog.Names.Count == 0)
        {
            return 0;
        }

        await using var scope = _scopes.CreateAsyncScope();
        var context = Context(scope);
        var now = _time.GetUtcNow();
        var abandoned = await JobRunTransitions.AbandonedAsync(context, _catalog.Names, now, RecoveryBatchSize, cancellationToken).ConfigureAwait(false);
        var recovered = 0;
        var error = StoredError.From(JobAttemptErrors.LeaseExpired);
        foreach (var run in abandoned)
        {
            var retry = _catalog.Find(run.JobName)!.Definition.Retry;
            var failures = run.FailureCount + 1;
            var retryAt = failures < retry.MaxAttempts ? now + retry.DelayAfter(failures) : (DateTimeOffset?)null;
            var (ending, recorded) = (run.CancelRequestedAt, retryAt) switch
            {
                ({ }, _) => (AttemptEnding.Cancelled(now), Recovered(run, JobRunEventKind.Cancelled, now, error, null)),
                (null, { } due) => (AttemptEnding.Failed(failures, due, error), Recovered(run, JobRunEventKind.LeaseExpired, now, error, due)),
                _ => (AttemptEnding.DeadLettered(failures, now, error), Recovered(run, JobRunEventKind.DeadLettered, now, error, null)),
            };
            if (await JobRunTransitions.EndAbandonedAsync(context, run, now, ending, recorded, cancellationToken).ConfigureAwait(false) is { } status)
            {
                recovered++;
                _metrics.LeaseRecovered(run.JobName);
                LogRecovered(run.JobName, run.AttemptCount, run.LeaseHolder, status);
            }
        }

        return recovered;
    }

    public Task WhenIdleAsync() => Task.WhenAll(_attempts.Values);

    public void Abort() => _abort.Cancel();

    public void Abandon() => _abandon.Cancel();

    public void Dispose()
    {
        _abort.Dispose();
        _abandon.Dispose();
    }

    private static JobsDbContext Context(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<JobsDbContext>();

    private static JobRunEvent Recovered(JobRun run, JobRunEventKind kind, DateTimeOffset at, StoredError error, DateTimeOffset? retryAt) =>
        JobRunEvent.Record(run.Id, kind, at, ActorIds.System, run.AttemptCount, run.LeaseHolder, error: error, retryAt: retryAt);

    private static JobRunEvent Recorded(ClaimedRun run, JobRunEventKind kind, DateTimeOffset at, StoredError? error = null, DateTimeOffset? retryAt = null) =>
        JobRunEvent.Record(run.Id, kind, at, ActorIds.System, run.Attempt, run.Runner, run.TraceId, error, retryAt);

    private static (AttemptEnding Ending, JobRunEvent Event) Ending(ClaimedRun run, JobRetryPolicy retry, JobAttemptOutcome outcome, DateTimeOffset now)
    {
        switch (outcome.End)
        {
            case JobAttemptEnd.Succeeded:
                return (AttemptEnding.Succeeded(now), Recorded(run, JobRunEventKind.Succeeded, now));
            case JobAttemptEnd.Interrupted:
                return (AttemptEnding.Interrupted(now), Recorded(run, JobRunEventKind.Interrupted, now));
            case JobAttemptEnd.Cancelled:
                return (AttemptEnding.Cancelled(now), Recorded(run, JobRunEventKind.Cancelled, now));
            default:
                var error = StoredError.From(outcome.Error!);
                var failures = run.FailureCount + 1;
                if (outcome.End == JobAttemptEnd.Retryable && failures < retry.MaxAttempts)
                {
                    var retryAt = now + retry.DelayAfter(failures);
                    return (AttemptEnding.Failed(failures, retryAt, error), Recorded(run, JobRunEventKind.Failed, now, error, retryAt));
                }

                return (AttemptEnding.DeadLettered(failures, now, error), Recorded(run, JobRunEventKind.DeadLettered, now, error));
        }
    }

    private void Start(string jobName, Func<Task> attempt, CancellationToken claimToken)
    {
        Interlocked.Increment(ref _slotsInUse);
        var id = Interlocked.Increment(ref _sequence);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _attempts[id] = finished.Task;
        _ = Task.Run(async () =>
        {
            try
            {
                await attempt().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (claimToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                LogAttemptBroke(exception, jobName);
            }
            finally
            {
                _attempts.TryRemove(id, out _);
                Interlocked.Decrement(ref _slotsInUse);
                finished.TrySetResult();
                _signal.Wake();
            }
        }, CancellationToken.None);
    }

    private async Task<CompletedAttempt?> ClaimAndRunAsync(JobRunId runId, int observedAttempts, string jobName, CancellationToken claimToken, CancellationToken jobToken)
    {
        using var activity = JobsTelemetry.StartAttempt(jobName);
        var run = await TryClaimAsync(runId, observedAttempts, activity.TraceId.ToHexString(), claimToken).ConfigureAwait(false);
        if (run is null)
        {
            activity.SetTag(JobsMetrics.OutcomeTag, "claim-lost");
            _metrics.ClaimLost(jobName);
            return null;
        }

        return await RunAttemptAsync(run, activity, jobToken).ConfigureAwait(false);
    }

    private async Task<CompletedAttempt> RunAttemptAsync(ClaimedRun run, Activity activity, CancellationToken jobToken)
    {
        var registration = _catalog.Find(run.JobName) ?? throw new InvalidOperationException($"The job {run.JobName} was claimed, but this release does not define it.");
        activity.SetTag(JobsTelemetry.RunIdTag, run.Id.Value.ToString());
        activity.SetTag(JobsTelemetry.AttemptTag, run.Attempt);
        using var logScope = _logger.BeginScope(new JobAttemptScope(run.Id, run.JobName, run.Attempt, run.TraceId));
        LogStarted(run.JobName, run.Attempt);
        _metrics.AttemptStarted(run.JobName);
        try
        {
            var started = _time.GetTimestamp();
            var outcome = await InvokeAsync(registration, run, jobToken).ConfigureAwait(false);
            var elapsed = _time.GetElapsedTime(started);
            (JobAttemptOutcome Outcome, JobRunStatus? Status, string Tag) recorded;
            try
            {
                recorded = await RecordAsync(run, registration.Definition.Retry, outcome, elapsed).ConfigureAwait(false);
            }
            catch
            {
                Measure(run.JobName, activity, outcome, UnrecordedOutcome, elapsed);
                throw;
            }

            Measure(run.JobName, activity, recorded.Outcome, recorded.Tag, elapsed);

            return new CompletedAttempt(run, recorded.Outcome, recorded.Status);
        }
        finally
        {
            _metrics.AttemptFinished(run.JobName);
        }
    }

    private async Task<JobAttemptOutcome> InvokeAsync(JobRegistration registration, ClaimedRun run, CancellationToken jobToken)
    {
        var definition = registration.Definition;
        Result? result = null;
        Exception? failure = null;
        bool leaseLost;
        bool cancelRequested;
        TimeSpan? overran;
        using (var timeout = new CancellationTokenSource(definition.AttemptTimeout, _time))
        {
            await using var lease = new JobLease(_scopes, run, _options.Value, _time, _logger);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource([_abort.Token, timeout.Token, lease.Token, jobToken]);
            using var overrun = timeout.Token.Register(() => LogOverran(run.JobName, run.Attempt, definition.AttemptTimeout));
            lease.Start();
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                result = await _invoker.InvokeAsync(registration, run.Payload, new JobContext(run.Id, run.Attempt), scope.ServiceProvider, linked.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            await lease.StopAsync().ConfigureAwait(false);
            leaseLost = lease.Lost;
            cancelRequested = lease.CancelRequested;
            overran = timeout.IsCancellationRequested ? definition.AttemptTimeout : null;
        }

        var outcome = JobAttemptOutcome.Classify(result, failure, leaseLost, cancelRequested, _abort.IsCancellationRequested, overran);
        if (failure is not null && outcome.Error?.Code == JobAttemptErrors.ExceptionCode)
        {
            LogThrew(failure, run.JobName, run.Attempt, failure.GetType().Name);
        }

        return outcome;
    }

    private async Task<(JobAttemptOutcome Outcome, JobRunStatus? Status, string Tag)> RecordAsync(ClaimedRun run, JobRetryPolicy retry, JobAttemptOutcome outcome, TimeSpan elapsed)
    {
        if (outcome.End == JobAttemptEnd.LeaseLost)
        {
            return (outcome, null, LeaseLostOutcome);
        }

        var now = _time.GetUtcNow();
        var (ending, recorded) = Ending(run, retry, outcome, now);
        JobRunStatus? written;
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            written = await JobRunTransitions.EndAttemptAsync(Context(scope), run, now, ending, recorded, _abandon.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_abandon.IsCancellationRequested)
        {
            LogRecordingAbandoned(run.JobName, run.Attempt);
            return (outcome, null, UnrecordedOutcome);
        }

        switch (written)
        {
            case JobRunStatus.Succeeded:
                LogSucceeded(run.JobName, run.Attempt, elapsed.TotalMilliseconds);
                return (outcome, written, "succeeded");
            case JobRunStatus.Failed:
                LogFailed(run.JobName, run.Attempt, recorded.ErrorCode!, recorded.RetryAt!.Value);
                return (outcome, written, "failed");
            case JobRunStatus.DeadLettered:
                LogGaveUp(run.JobName, run.Attempt, recorded.ErrorCode!);
                return (outcome, written, "dead-lettered");
            case JobRunStatus.Pending:
                LogInterrupted(run.JobName, run.Attempt);
                return (outcome, written, "interrupted");
            case JobRunStatus.Cancelled:
                LogCancelled(run.JobName, run.Attempt);
                return (new JobAttemptOutcome(JobAttemptEnd.Cancelled, null), written, "cancelled");
            default:
                LogOutcomeDiscarded(run.JobName, run.Attempt);
                return (outcome, null, LeaseLostOutcome);
        }
    }

    private void Measure(string jobName, Activity activity, JobAttemptOutcome outcome, string tag, TimeSpan elapsed)
    {
        var storedCode = outcome.Error is { } error ? StoredError.From(error).Code : null;
        _metrics.AttemptEnded(jobName, tag, storedCode, elapsed);
        activity.SetTag(JobsMetrics.OutcomeTag, tag);
        if (storedCode is not null)
        {
            activity.SetTag(JobsMetrics.ErrorCodeTag, storedCode);
            activity.SetStatus(ActivityStatusCode.Error, storedCode);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Started attempt {Attempt} of {JobName}")]
    private partial void LogStarted(string jobName, int attempt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Attempt {Attempt} of {JobName} succeeded in {ElapsedMs:0.0} ms")]
    private partial void LogSucceeded(string jobName, int attempt, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Attempt {Attempt} of {JobName} failed with {ErrorCode}; the run tries again at {RetryAt}")]
    private partial void LogFailed(string jobName, int attempt, string errorCode, DateTimeOffset retryAt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Attempt {Attempt} of {JobName} failed with {ErrorCode} and the run gave up; an administrator can run it again from the jobs page")]
    private partial void LogGaveUp(string jobName, int attempt, string errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Attempt {Attempt} of {JobName} threw {ExceptionType}")]
    private partial void LogThrew(Exception exception, string jobName, int attempt, string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Attempt {Attempt} of {JobName} ran past its limit of {Limit}; its job was told to stop and keeps its slot until it returns")]
    private partial void LogOverran(string jobName, int attempt, TimeSpan limit);

    [LoggerMessage(Level = LogLevel.Information, Message = "Attempt {Attempt} of {JobName} was interrupted by the shutdown; the run waits for the next start")]
    private partial void LogInterrupted(string jobName, int attempt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Attempt {Attempt} of {JobName} stopped at an administrator's request; the run is cancelled")]
    private partial void LogCancelled(string jobName, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Attempt {Attempt} of {JobName} no longer holds its lease; its outcome is discarded")]
    private partial void LogOutcomeDiscarded(string jobName, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Attempt {Attempt} of {JobName} could not record its outcome before the shutdown ended; its run is recovered once its lease expires")]
    private partial void LogRecordingAbandoned(string jobName, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recovered attempt {Attempt} of {JobName} from {Runner}, which stopped renewing its lease; the run is now {Status}")]
    private partial void LogRecovered(string jobName, int attempt, string? runner, JobRunStatus status);

    [LoggerMessage(Level = LogLevel.Error, Message = "An attempt of {JobName} stopped on an error of the runner; its run is claimed again once it is due or its lease has expired")]
    private partial void LogAttemptBroke(Exception exception, string jobName);
}
