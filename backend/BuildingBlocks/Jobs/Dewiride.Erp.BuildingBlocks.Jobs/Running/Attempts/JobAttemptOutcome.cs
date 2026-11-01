using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Running.Attempts;

// Only failures that can heal are retried: a dependency that could not answer, a conflict with a concurrent change, an exception,
// an attempt that overran its limit or lost its runner. A refusal that cannot change, such as a validation error or a missing
// permission, gives up at once, because retrying it only delays the dead letter that tells an administrator to act. A job that
// succeeds counts even when it was asked to stop; a lost lease discards whatever the attempt did, since another runner owns the
// run; a cancel request wins over the shutdown that would otherwise put the run back.
internal readonly record struct JobAttemptOutcome(JobAttemptEnd End, Error? Error)
{
    public static JobAttemptOutcome Classify(Result? result, Exception? exception, bool leaseLost, bool cancelRequested, bool interrupted, TimeSpan? overranLimit)
    {
        if (leaseLost)
        {
            return new JobAttemptOutcome(JobAttemptEnd.LeaseLost, null);
        }

        if (exception is null && result is { IsSuccess: true })
        {
            return new JobAttemptOutcome(JobAttemptEnd.Succeeded, null);
        }

        if (cancelRequested)
        {
            return new JobAttemptOutcome(JobAttemptEnd.Cancelled, null);
        }

        if (interrupted)
        {
            return new JobAttemptOutcome(JobAttemptEnd.Interrupted, null);
        }

        if (overranLimit is { } limit)
        {
            return new JobAttemptOutcome(JobAttemptEnd.Retryable, JobAttemptErrors.TimedOut(limit));
        }

        if (exception is not null)
        {
            return new JobAttemptOutcome(JobAttemptEnd.Retryable, JobAttemptErrors.Threw(exception));
        }

        var error = result?.Error ?? throw new ArgumentException("An attempt ends with a result or an exception.", nameof(result));

        return error.Kind is ErrorKind.Unavailable or ErrorKind.Conflict
            ? new JobAttemptOutcome(JobAttemptEnd.Retryable, error)
            : new JobAttemptOutcome(JobAttemptEnd.Permanent, error);
    }
}
