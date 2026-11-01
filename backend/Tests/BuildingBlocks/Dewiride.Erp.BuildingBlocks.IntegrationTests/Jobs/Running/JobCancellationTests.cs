using System.Diagnostics;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Running;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Testing.Jobs;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Running;

// The second runner's host stands for the API an administrator uses, its actor the administrator; the first runs the jobs as
// the system.
public sealed class JobCancellationTests
{
    private static readonly Guid Administrator = new("0198f6a2-7c1e-7b3a-9d4e-000000000a01");

    [Fact]
    public async Task CancelAsync_Pending_CancelsAtOnce()
    {
        await using var jobs = Create(JobScript.Succeeding(), "cancel-pending");
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());

        var cancelled = await AdministerAsync(jobs, administration => administration.CancelAsync(id, TestContext.Current.CancellationToken));

        Assert.Equal(JobRunStatus.Cancelled, cancelled.Value.Status);
        var run = await jobs.RunAsync(id);
        Assert.Equal((Administrator, jobs.SecondClock.GetUtcNow()), (run.CancelRequestedBy!.Value, run.FinishedAt!.Value));
        Assert.Equal(Administrator, Assert.Single(await jobs.EventsAsync(id), e => e.Kind == JobRunEventKind.Cancelled).ActorId);
        Assert.Null(await jobs.FirstRunner.TryRunAsync(id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancelAsync_Running_StopsTheJobAtItsNextRenewal()
    {
        var script = JobScript.Succeeding().WaitingForItsToken();
        await using var jobs = Create(script, "cancel-running");
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        var attempt = new JobRunnerDriver(jobs.First.Services).RunAsync(id, TestContext.Current.CancellationToken);
        await script.Started;

        var requested = await AdministerAsync(jobs, administration => administration.CancelAsync(id, TestContext.Current.CancellationToken));
        var repeated = await AdministerAsync(jobs, administration => administration.CancelAsync(id, TestContext.Current.CancellationToken));
        Assert.Equal(JobRunStatus.Running, requested.Value.Status);
        Assert.Equal(requested.Value.CancelRequestedAt, repeated.Value.CancelRequestedAt);
        Assert.False(script.TokenFired);
        jobs.FirstClock.Advance(RenewalInterval(jobs));
        var outcome = await attempt;

        Assert.True(script.TokenFired);
        Assert.Equal(JobRunStatus.Cancelled, outcome.Status);
        var events = await jobs.EventsAsync(id);
        Assert.Equal(Administrator, Assert.Single(events, e => e.Kind == JobRunEventKind.CancelRequested).ActorId);
        Assert.Equal(ActorIds.System, Assert.Single(events, e => e.Kind == JobRunEventKind.Cancelled).ActorId);
    }

    [Fact]
    public async Task CancelAsync_RunningJobFinishesFirst_RecordsSucceeded()
    {
        var script = JobScript.Succeeding().WaitingForRelease();
        await using var jobs = Create(script, "cancel-late");
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        var attempt = new JobRunnerDriver(jobs.First.Services).RunAsync(id, TestContext.Current.CancellationToken);
        await script.Started;

        await AdministerAsync(jobs, administration => administration.CancelAsync(id, TestContext.Current.CancellationToken));
        script.Release();
        var outcome = await attempt;

        Assert.Equal(JobRunStatus.Succeeded, outcome.Status);
    }

    [Fact]
    public async Task CancelAsync_RunningWhoseRunnerStopped_CancelsAtOnce()
    {
        await using var jobs = Create(JobScript.Succeeding(), "cancel-abandoned");
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        await jobs.FirstRunner.TryClaimAsync(id, 0, ActivityTraceId.CreateRandom().ToHexString(), TestContext.Current.CancellationToken);
        jobs.SecondClock.Advance(jobs.Second.Services.GetRequiredService<IOptions<JobRunnerOptions>>().Value.LeaseDuration);

        var cancelled = await AdministerAsync(jobs, administration => administration.CancelAsync(id, TestContext.Current.CancellationToken));

        Assert.Equal(JobRunStatus.Cancelled, cancelled.Value.Status);
        var run = await jobs.RunAsync(id);
        Assert.Null(run.LeaseToken);
        var cancel = Assert.Single(await jobs.EventsAsync(id), e => e.Kind == JobRunEventKind.Cancelled);
        Assert.Equal((Administrator, "job.lease-expired", 1), (cancel.ActorId, cancel.ErrorCode, cancel.Attempt!.Value));
    }

    [Fact]
    public async Task CancelAsync_Finished_IsRefused()
    {
        await using var jobs = Create(JobScript.Succeeding(), "cancel-finished");
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        await new JobRunnerDriver(jobs.First.Services).RunAsync(id, TestContext.Current.CancellationToken);

        var refused = await AdministerAsync(jobs, administration => administration.CancelAsync(id, TestContext.Current.CancellationToken));

        Assert.Equal(JobErrors.RunFinished, refused.Error);
    }

    [Fact]
    public async Task RerunAsync_DeadLettered_ResetsTheBudgetAndRecordsTheAdministrator()
    {
        var script = JobScript.Failing(Error.Validation("test.invalid", "The input is invalid."));
        await using var jobs = Create(script, "rerun");
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        var driver = new JobRunnerDriver(jobs.First.Services);
        Assert.Equal(JobRunStatus.DeadLettered, (await driver.RunAsync(id, TestContext.Current.CancellationToken)).Status);
        jobs.Advance(TimeSpan.FromMinutes(5));

        var rerun = await AdministerAsync(jobs, administration => administration.RerunAsync(id, TestContext.Current.CancellationToken));

        Assert.Equal((JobRunStatus.Pending, 0, 1, (string?)null, (DateTimeOffset?)null), (rerun.Value.Status, rerun.Value.FailureCount, rerun.Value.AttemptCount, rerun.Value.LastErrorCode, rerun.Value.FinishedAt));
        Assert.Equal(jobs.SecondClock.GetUtcNow(), rerun.Value.DueAt);
        Assert.Equal(Administrator, Assert.Single(await jobs.EventsAsync(id), e => e.Kind == JobRunEventKind.Rerun).ActorId);
        script.Run = (_, _) => Task.FromResult(Result.Success());
        var second = await driver.RunAsync(id, TestContext.Current.CancellationToken);
        Assert.Equal(JobRunStatus.Succeeded, second.Status);
        Assert.Equal(2, (await jobs.RunAsync(id)).AttemptCount);
    }

    [Fact]
    public async Task RerunAsync_Succeeded_IsRefused()
    {
        await using var jobs = Create(JobScript.Succeeding(), "rerun-succeeded");
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        await new JobRunnerDriver(jobs.First.Services).RunAsync(id, TestContext.Current.CancellationToken);

        var refused = await AdministerAsync(jobs, administration => administration.RerunAsync(id, TestContext.Current.CancellationToken));

        Assert.Equal(JobErrors.RunNotRerunnable, refused.Error);
        Assert.DoesNotContain(await jobs.EventsAsync(id), e => e.Kind == JobRunEventKind.Rerun);
    }

    [Fact]
    public async Task RerunAsync_UnknownRun_IsNotFound()
    {
        await using var jobs = Create(JobScript.Succeeding(), "rerun-unknown");

        var refused = await AdministerAsync(jobs, administration => administration.RerunAsync(JobRunId.Create(), TestContext.Current.CancellationToken));

        Assert.Equal(JobErrors.RunNotFound, refused.Error);
    }

    private static JobsDatabase Create(JobScript script, string scenario)
    {
        var jobs = JobsDatabase.Create(services => services.AddSingleton(script).AddJob<ScriptedJob>(JobsDatabase.Definition(JobsDatabase.NewName(scenario))));
        JobsDatabase.ActorOf(jobs.Second).ActorId = Administrator;

        return jobs;
    }

    private static async Task<Result<JobRunDetails>> AdministerAsync(JobsDatabase jobs, Func<IJobAdministration, Task<Result<JobRunDetails>>> action)
    {
        await using var scope = jobs.Second.Services.CreateAsyncScope();

        return await action(scope.ServiceProvider.GetRequiredService<IJobAdministration>());
    }

    private static TimeSpan RenewalInterval(JobsDatabase jobs) => jobs.First.Services.GetRequiredService<IOptions<JobRunnerOptions>>().Value.RenewalInterval;
}
