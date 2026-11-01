using System.Diagnostics;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Telemetry;

// Every attempt runs in a trace of its own, sampled or not, because its trace id is the correlation id of the attempt's logs,
// of its events and of the change records the job makes as the system.
internal static class JobsTelemetry
{
    public const string ActivitySourceName = "Dewiride.Erp.Jobs";

    public const string RunIdTag = "erp.jobs.run_id";

    public const string AttemptTag = "erp.jobs.attempt";

    private static readonly ActivitySource Source = new(ActivitySourceName);

    public static Activity StartAttempt(string jobName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);

        var name = $"job {jobName}";
        var activity = Source.StartActivity(name, ActivityKind.Internal) ?? new Activity(name).SetIdFormat(ActivityIdFormat.W3C).Start();
        activity.SetTag(JobsMetrics.JobTag, jobName);

        return activity;
    }
}
