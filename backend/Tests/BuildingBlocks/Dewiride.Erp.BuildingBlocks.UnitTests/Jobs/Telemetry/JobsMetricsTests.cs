using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dewiride.Erp.BuildingBlocks.Jobs.Telemetry;
using Dewiride.Erp.BuildingBlocks.Observability.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Jobs.Telemetry;

public sealed class JobsMetricsTests : IDisposable
{
    private const string Job = "test.jobs.metrics";

    private readonly ServiceProvider _services = new ServiceCollection().AddMetrics().BuildServiceProvider();

    private IMeterFactory MeterFactory => _services.GetRequiredService<IMeterFactory>();

    [Fact]
    public void AttemptEnded_FailedAttempt_CountsItByJobOutcomeAndCodeAndRecordsItsDuration()
    {
        using var attempts = Collect<long>("erp.jobs.attempts");
        using var durations = Collect<double>("erp.jobs.attempt.duration");
        using var active = Collect<long>("erp.jobs.attempts.active");
        var metrics = new JobsMetrics(MeterFactory);

        metrics.AttemptStarted(Job);
        metrics.AttemptEnded(Job, "failed", "directory.unavailable", TimeSpan.FromMilliseconds(1_500));
        var activeBeforeTheAttemptFinished = active.GetMeasurementSnapshot().Select(measurement => measurement.Value).ToList();
        metrics.AttemptFinished(Job);

        Assert.Equal([1L], activeBeforeTheAttemptFinished);
        var attempt = Assert.Single(attempts.GetMeasurementSnapshot());
        Assert.Equal(1, attempt.Value);
        Assert.Equal(Job, attempt.Tags[JobsMetrics.JobTag]);
        Assert.Equal("failed", attempt.Tags[JobsMetrics.OutcomeTag]);
        Assert.Equal("directory.unavailable", attempt.Tags[JobsMetrics.ErrorCodeTag]);
        var duration = Assert.Single(durations.GetMeasurementSnapshot());
        Assert.Equal(1.5, duration.Value);
        Assert.Equal("s", durations.Instrument!.Unit);
        Assert.False(duration.Tags.ContainsKey(JobsMetrics.ErrorCodeTag));
        Assert.Equal([1L, -1L], active.GetMeasurementSnapshot().Select(measurement => measurement.Value));
    }

    [Fact]
    public void AttemptEnded_Success_CarriesNoErrorCode()
    {
        using var attempts = Collect<long>("erp.jobs.attempts");
        var metrics = new JobsMetrics(MeterFactory);

        metrics.AttemptEnded(Job, "succeeded", null, TimeSpan.FromSeconds(1));

        Assert.False(Assert.Single(attempts.GetMeasurementSnapshot()).Tags.ContainsKey(JobsMetrics.ErrorCodeTag));
    }

    [Fact]
    public void Occurrences_EnqueuedAndSkipped_AreCountedByOutcome()
    {
        using var occurrences = Collect<long>("erp.jobs.occurrences");
        var metrics = new JobsMetrics(MeterFactory);

        metrics.OccurrenceEnqueued(Job);
        metrics.OccurrenceSkipped(Job);

        Assert.Equal(["enqueued", "skipped"], occurrences.GetMeasurementSnapshot().Select(measurement => measurement.Tags[JobsMetrics.OutcomeTag]));
    }

    [Fact]
    public void Enqueued_CreatedAndExisting_AreCountedByOutcome()
    {
        using var enqueued = Collect<long>("erp.jobs.enqueued");
        var metrics = new JobsMetrics(MeterFactory);

        metrics.Enqueued(Job, created: true);
        metrics.Enqueued(Job, created: false);

        Assert.Equal(["created", "existing"], enqueued.GetMeasurementSnapshot().Select(measurement => measurement.Tags[JobsMetrics.OutcomeTag]));
    }

    [Fact]
    public void LeasesAndClaims_Counts_AreTaggedWithTheJob()
    {
        using var recovered = Collect<long>("erp.jobs.leases.recovered");
        using var lost = Collect<long>("erp.jobs.claims.lost");
        var metrics = new JobsMetrics(MeterFactory);

        metrics.LeaseRecovered(Job);
        metrics.ClaimLost(Job);

        Assert.Equal(Job, Assert.Single(recovered.GetMeasurementSnapshot()).Tags[JobsMetrics.JobTag]);
        Assert.Equal(Job, Assert.Single(lost.GetMeasurementSnapshot()).Tags[JobsMetrics.JobTag]);
    }

    [Fact]
    public void MeterAndActivitySource_TelemetryBaseline_ExportsThem()
    {
        Assert.Contains(TelemetryMeters.Names, name => name.EndsWith(".*", StringComparison.Ordinal) && JobsMetrics.MeterName.StartsWith(name[..^1], StringComparison.Ordinal));
        Assert.StartsWith(OpenTelemetrySetup.ActivitySourcePrefix + ".", JobsTelemetry.ActivitySourceName, StringComparison.Ordinal);
    }

    [Fact]
    public void StartAttempt_NoListener_StillStartsAW3CTrace()
    {
        using var activity = JobsTelemetry.StartAttempt(Job);

        Assert.NotNull(Activity.Current);
        Assert.Equal(ActivityIdFormat.W3C, activity.IdFormat);
        Assert.Equal(32, activity.TraceId.ToHexString().Length);
        Assert.Equal(Job, activity.GetTagItem(JobsMetrics.JobTag));
    }

    public void Dispose() => _services.Dispose();

    private MetricCollector<T> Collect<T>(string instrument)
        where T : struct => new(MeterFactory, JobsMetrics.MeterName, instrument);
}
