using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Domain;

// One row for each thing that happened to a run: who asked for it, re-ran or cancelled it, each attempt's start and outcome with
// its trace, which is also the correlation id of the change records the attempt made. Inserted once, never changed, and deleted
// only with its run.
internal sealed class JobRunEvent : Entity<JobRunEventId>, IAppendOnly
{
    public const int TraceIdLength = 32;

    private JobRunEvent(JobRunEventId id)
        : base(id)
    {
    }

    public JobRunId RunId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public JobRunEventKind Kind { get; private set; }

    public Guid ActorId { get; private set; }

    public int? Attempt { get; private set; }

    public string? Runner { get; private set; }

    public string? TraceId { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset? RetryAt { get; private set; }

    public static JobRunEvent Record(
        JobRunId runId,
        JobRunEventKind kind,
        DateTimeOffset occurredAt,
        Guid actorId,
        int? attempt = null,
        string? runner = null,
        string? traceId = null,
        StoredError? error = null,
        DateTimeOffset? retryAt = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The kind of a job run event must be one of JobRunEventKind.");
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(runner?.Length ?? 0, JobRun.LeaseHolderMaxLength, nameof(runner));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(traceId?.Length ?? 0, TraceIdLength, nameof(traceId));

        return new JobRunEvent(JobRunEventId.Create())
        {
            RunId = runId,
            Kind = kind,
            OccurredAt = occurredAt,
            ActorId = actorId,
            Attempt = attempt,
            Runner = runner,
            TraceId = traceId,
            ErrorCode = error?.Code,
            ErrorMessage = error?.Message,
            RetryAt = retryAt,
        };
    }
}
