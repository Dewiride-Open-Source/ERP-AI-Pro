using System.Diagnostics.Metrics;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Telemetry;

internal sealed class JobsMetrics
{
    public const string MeterName = "Dewiride.Erp.Jobs";

    public const string JobTag = "erp.jobs.job";

    public const string OutcomeTag = "erp.jobs.outcome";

    public const string ErrorCodeTag = "erp.error.code";

    private readonly Counter<long> _attempts;
    private readonly Histogram<double> _attemptDuration;
    private readonly UpDownCounter<long> _activeAttempts;
    private readonly Counter<long> _recoveredLeases;
    private readonly Counter<long> _lostClaims;
    private readonly Counter<long> _occurrences;
    private readonly Counter<long> _enqueued;

    public JobsMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(MeterName);
        _attempts = meter.CreateCounter<long>("erp.jobs.attempts", "{attempt}", "Attempts by job and outcome: succeeded, failed, dead-lettered, interrupted, cancelled, lease-lost, or unrecorded when the outcome could not be written.");
        _attemptDuration = meter.CreateHistogram<double>("erp.jobs.attempt.duration", "s", "Duration of attempts by job and outcome.");
        _activeAttempts = meter.CreateUpDownCounter<long>("erp.jobs.attempts.active", "{attempt}", "Attempts this process is running.");
        _recoveredLeases = meter.CreateCounter<long>("erp.jobs.leases.recovered", "{lease}", "Runs recovered after the runner holding them stopped renewing their lease.");
        _lostClaims = meter.CreateCounter<long>("erp.jobs.claims.lost", "{claim}", "Claims of a due run that another runner made first.");
        _occurrences = meter.CreateCounter<long>("erp.jobs.occurrences", "{occurrence}", "Scheduled occurrences by outcome: enqueued, or skipped while the previous run is unfinished.");
        _enqueued = meter.CreateCounter<long>("erp.jobs.enqueued", "{run}", "Enqueues by outcome: created, or existing when the job already used the key.");
    }

    public void AttemptStarted(string job) => _activeAttempts.Add(1, Tag(JobTag, job));

    public void AttemptFinished(string job) => _activeAttempts.Add(-1, Tag(JobTag, job));

    public void AttemptEnded(string job, string outcome, string? errorCode, TimeSpan duration)
    {
        _attemptDuration.Record(duration.TotalSeconds, Tag(JobTag, job), Tag(OutcomeTag, outcome));
        if (errorCode is null)
        {
            _attempts.Add(1, Tag(JobTag, job), Tag(OutcomeTag, outcome));
        }
        else
        {
            _attempts.Add(1, Tag(JobTag, job), Tag(OutcomeTag, outcome), Tag(ErrorCodeTag, errorCode));
        }
    }

    public void LeaseRecovered(string job) => _recoveredLeases.Add(1, Tag(JobTag, job));

    public void ClaimLost(string job) => _lostClaims.Add(1, Tag(JobTag, job));

    public void OccurrenceEnqueued(string job) => _occurrences.Add(1, Tag(JobTag, job), Tag(OutcomeTag, "enqueued"));

    public void OccurrenceSkipped(string job) => _occurrences.Add(1, Tag(JobTag, job), Tag(OutcomeTag, "skipped"));

    public void Enqueued(string job, bool created) => _enqueued.Add(1, Tag(JobTag, job), Tag(OutcomeTag, created ? "created" : "existing"));

    private static KeyValuePair<string, object?> Tag(string name, string value) => new(name, value);
}
