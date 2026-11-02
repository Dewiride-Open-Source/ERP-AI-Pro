using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Running.Attempts;

// An exception is kept as its type name only: its message can quote a value, as SQL Server's messages do, or an address that
// carries a token. The exception itself goes to the log of the attempt.
internal static class JobAttemptErrors
{
    public const string ExceptionCode = "job.exception";

    public const string TimedOutCode = "job.timed-out";

    public static readonly Error LeaseExpired = Error.Unavailable("job.lease-expired", "The runner of the attempt stopped renewing its lease, so the attempt counts as failed.");

    public static readonly Error PayloadUnreadable = Error.Failure("job.payload-unreadable", "The input of the run can no longer be read, so the run cannot run.");

    public static readonly Error ActorNotSystem = Error.Failure("job.actor-not-system", "The attempt would have run as someone other than the ERP itself, so it did not run.");

    public static Error TimedOut(TimeSpan limit) => Error.Unavailable(TimedOutCode, $"The attempt ran longer than its limit of {limit:c} and was told to stop.");

    public static Error Threw(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return Error.Unavailable(ExceptionCode, $"The attempt threw {exception.GetType().Name}; the log of the attempt holds the details.");
    }
}
