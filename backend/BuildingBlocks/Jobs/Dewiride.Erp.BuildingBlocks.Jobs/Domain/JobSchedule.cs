namespace Dewiride.Erp.BuildingBlocks.Jobs.Domain;

// The state of one recurring job's schedule: the next occurrence, computed from the normalised expression the row keeps, and the
// last occurrence that was enqueued with its run. The schedule itself lives in code and configuration, so the row is neither
// auditable nor append-only, and LastRunId has no foreign key: a run the retention sweep deleted reads as finished.
internal sealed class JobSchedule
{
    public const int ExpressionMaxLength = 200;

    private JobSchedule()
    {
    }

    public string JobName { get; private set; } = string.Empty;

    public string Expression { get; private set; } = string.Empty;

    public DateTimeOffset NextOccurrenceAt { get; private set; }

    public DateTimeOffset? LastOccurrenceAt { get; private set; }

    public JobRunId? LastRunId { get; private set; }

    public static JobSchedule Start(string jobName, string expression, DateTimeOffset nextOccurrenceAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);

        return new JobSchedule { JobName = jobName, Expression = expression, NextOccurrenceAt = nextOccurrenceAt };
    }
}
