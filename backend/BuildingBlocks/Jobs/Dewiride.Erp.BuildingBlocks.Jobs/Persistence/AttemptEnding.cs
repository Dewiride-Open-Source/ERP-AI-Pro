using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Persistence;

// The state a run takes when its attempt ends. An interrupted attempt puts the run back as due at once without counting a
// failure, because a restart is no failure; a success clears the error an earlier attempt left.
internal readonly record struct AttemptEnding(JobRunStatus Status, DateTimeOffset? DueAt, int? FailureCount, DateTimeOffset? FinishedAt, StoredError? Error, bool ClearsError)
{
    public static AttemptEnding Succeeded(DateTimeOffset at) => new(JobRunStatus.Succeeded, null, null, at, null, ClearsError: true);

    public static AttemptEnding Failed(int failures, DateTimeOffset retryAt, StoredError error) => new(JobRunStatus.Failed, retryAt, failures, null, error, ClearsError: false);

    public static AttemptEnding DeadLettered(int failures, DateTimeOffset at, StoredError error) => new(JobRunStatus.DeadLettered, null, failures, at, error, ClearsError: false);

    public static AttemptEnding Interrupted(DateTimeOffset at) => new(JobRunStatus.Pending, at, null, null, null, ClearsError: false);

    public static AttemptEnding Cancelled(DateTimeOffset at) => new(JobRunStatus.Cancelled, null, null, at, null, ClearsError: false);
}
