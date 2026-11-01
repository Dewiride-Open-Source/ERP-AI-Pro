using System.Diagnostics.Metrics;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Telemetry;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Running;

public sealed class JobAttemptTelemetryTests
{
    [Fact]
    public async Task TryRunAsync_OutcomeCannotBeWritten_MeasuresTheAttemptUnrecordedAndNoLongerActive()
    {
        var script = JobScript.Succeeding().WaitingForRelease();
        var name = JobsDatabase.NewName("unrecorded");
        await using var jobs = JobsDatabase.Create(services => JobsTransactionHook.Register(services.AddSingleton(script).AddJob<ScriptedJob>(JobsDatabase.Definition(name))));
        var meters = jobs.First.Services.GetRequiredService<IMeterFactory>();
        using var active = new MetricCollector<long>(meters, JobsMetrics.MeterName, "erp.jobs.attempts.active");
        using var attempts = new MetricCollector<long>(meters, JobsMetrics.MeterName, "erp.jobs.attempts");
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        var attempt = jobs.FirstRunner.TryRunAsync(id, TestContext.Current.CancellationToken);
        await script.Started;

        JobsTransactionHook.Of(jobs.First).BeforeNextStart(() => throw new InvalidOperationException("The database refused the outcome."));
        script.Release();

        await Assert.ThrowsAsync<InvalidOperationException>(() => attempt);
        Assert.Equal([1L, -1L], active.GetMeasurementSnapshot().Where(measurement => Equals(measurement.Tags[JobsMetrics.JobTag], name)).Select(measurement => measurement.Value));
        var counted = Assert.Single(attempts.GetMeasurementSnapshot(), measurement => Equals(measurement.Tags[JobsMetrics.JobTag], name));
        Assert.Equal("unrecorded", counted.Tags[JobsMetrics.OutcomeTag]);
        Assert.Equal(JobRunStatus.Running, (await jobs.RunAsync(id)).Status);
    }
}
