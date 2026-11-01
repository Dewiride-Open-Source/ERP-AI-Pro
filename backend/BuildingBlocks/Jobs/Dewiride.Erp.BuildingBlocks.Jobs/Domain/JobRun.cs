using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Domain;

// Neither auditable nor append-only: a run changes at every claim, lease renewal and outcome, which as change records would fill
// the books' trail, so its history is kept in its events. After the insert it changes only through the compare-and-set
// statements of JobRunTransitions, never through the change tracker. AttemptCount numbers the attempts over the run's life,
// while FailureCount counts the failures since the enqueue or the last re-run and decides when the run gives up.
internal sealed class JobRun : Entity<JobRunId>
{
    public const int ErrorCodeMaxLength = 128;

    public const int ErrorMessageMaxLength = 512;

    public const int LeaseHolderMaxLength = 64;

    private JobRun(JobRunId id)
        : base(id)
    {
    }

    public string JobName { get; private set; } = string.Empty;

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string? Payload { get; private set; }

    public JobRunStatus Status { get; private set; }

    public DateTimeOffset DueAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid RequestedBy { get; private set; }

    public int AttemptCount { get; private set; }

    public int FailureCount { get; private set; }

    public Guid? LeaseToken { get; private set; }

    public string? LeaseHolder { get; private set; }

    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    public DateTimeOffset? CancelRequestedAt { get; private set; }

    public Guid? CancelRequestedBy { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public string? LastErrorCode { get; private set; }

    public string? LastErrorMessage { get; private set; }

    public static JobRun Enqueue(JobRunId id, string jobName, string idempotencyKey, string? payload, DateTimeOffset dueAt, DateTimeOffset createdAt, Guid requestedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        return new JobRun(id)
        {
            JobName = jobName,
            IdempotencyKey = idempotencyKey,
            Payload = payload,
            Status = JobRunStatus.Pending,
            DueAt = dueAt,
            CreatedAt = createdAt,
            RequestedBy = requestedBy,
        };
    }
}
