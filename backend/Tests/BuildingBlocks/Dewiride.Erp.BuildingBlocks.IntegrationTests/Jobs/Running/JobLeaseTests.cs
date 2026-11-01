using System.Diagnostics;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Running;
using Dewiride.Erp.Testing.Jobs;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Running;

// A runner that stops renewing is the first runner with its clock held still, so its renewals never fall due, while the second
// runner's clock moves past the lease.
public sealed class JobLeaseTests
{
    [Fact]
    public async Task RecoverExpiredLeasesAsync_RunnerStoppedRenewing_FailsTheAttemptAndSchedulesTheRetry()
    {
        var name = JobsDatabase.NewName("recover");
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(JobScript.Succeeding()).AddJob<ScriptedJob>(JobsDatabase.Definition(name)));
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        var claim = await jobs.FirstRunner.TryClaimAsync(id, 0, ActivityTraceId.CreateRandom().ToHexString(), TestContext.Current.CancellationToken);
        var lease = LeaseDuration(jobs);

        jobs.Advance(lease - TimeSpan.FromSeconds(1));
        Assert.Equal(0, await jobs.SecondRunner.RecoverExpiredLeasesAsync(TestContext.Current.CancellationToken));
        jobs.Advance(TimeSpan.FromSeconds(1));
        var recovered = await jobs.SecondRunner.RecoverExpiredLeasesAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(claim);
        Assert.Equal(1, recovered);
        var now = jobs.SecondClock.GetUtcNow();
        var run = await jobs.RunAsync(id);
        Assert.Equal((JobRunStatus.Failed, 1, 1), (run.Status, run.AttemptCount, run.FailureCount));
        Assert.Equal(now + JobRetryPolicy.Default.DelayAfter(1), run.DueAt);
        Assert.Equal(("job.lease-expired", (Guid?)null, (string?)null, (DateTimeOffset?)null), (run.LastErrorCode, run.LeaseToken, run.LeaseHolder, run.LeaseExpiresAt));
        var expired = Assert.Single(await jobs.EventsAsync(id), e => e.Kind == JobRunEventKind.LeaseExpired);
        Assert.Equal((1, "runner-a", "job.lease-expired", run.DueAt), (expired.Attempt!.Value, expired.Runner, expired.ErrorCode, expired.RetryAt!.Value));
    }

    [Fact]
    public async Task RenewAsync_LeaseHeld_ExtendsTheLeaseSoNoRunnerRecoversTheRun()
    {
        var script = JobScript.Succeeding().WaitingForRelease();
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(script).AddJob<ScriptedJob>(JobsDatabase.Definition(JobsDatabase.NewName("renew"))));
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        var claimedUntil = jobs.FirstClock.GetUtcNow() + LeaseDuration(jobs);
        var attempt = jobs.FirstRunner.TryRunAsync(id, TestContext.Current.CancellationToken);
        await script.Started;
        Assert.Equal(claimedUntil, (await jobs.RunAsync(id)).LeaseExpiresAt);

        jobs.FirstClock.Advance(RenewalInterval(jobs));
        var renewedUntil = jobs.FirstClock.GetUtcNow() + LeaseDuration(jobs);
        var waited = Stopwatch.StartNew();
        while ((await jobs.RunAsync(id)).LeaseExpiresAt != renewedUntil)
        {
            Assert.True(waited.Elapsed < TimeSpan.FromMinutes(1), "The renewal did not extend the lease.");
        }

        jobs.SecondClock.SetUtcNow(claimedUntil + ((renewedUntil - claimedUntil) / 2));
        var recovered = await new JobRunnerDriver(jobs.Second.Services).RecoverExpiredLeasesAsync(TestContext.Current.CancellationToken);
        script.Release();
        var completed = await attempt;

        Assert.Equal(0, recovered);
        Assert.Equal(JobRunStatus.Succeeded, completed!.RecordedStatus);
        Assert.DoesNotContain(await jobs.EventsAsync(id), e => e.Kind == JobRunEventKind.LeaseExpired);
    }

    [Fact]
    public async Task TryRunAsync_LeaseRecoveredBeforeTheJobReturns_DiscardsTheOutcome()
    {
        var script = JobScript.Succeeding().WaitingForRelease();
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(script).AddJob<ScriptedJob>(JobsDatabase.Definition(JobsDatabase.NewName("discard"))));
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        var attempt = jobs.FirstRunner.TryRunAsync(id, TestContext.Current.CancellationToken);
        await script.Started;

        jobs.SecondClock.Advance(LeaseDuration(jobs));
        Assert.Equal(1, await jobs.SecondRunner.RecoverExpiredLeasesAsync(TestContext.Current.CancellationToken));
        script.Release();
        var completed = await attempt;

        Assert.Null(completed!.RecordedStatus);
        var run = await jobs.RunAsync(id);
        Assert.Equal((JobRunStatus.Failed, 1), (run.Status, run.FailureCount));
        Assert.DoesNotContain(await jobs.EventsAsync(id), e => e.Kind == JobRunEventKind.Succeeded);
        Assert.Contains(JobsDatabase.LogsOf(jobs.First).GetSnapshot(), record => record.Level == LogLevel.Warning && record.Message.Contains("no longer holds its lease", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RenewAsync_LeaseLost_CancelsTheJob()
    {
        var script = JobScript.Succeeding().WaitingForItsToken();
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(script).AddJob<ScriptedJob>(JobsDatabase.Definition(JobsDatabase.NewName("lost"))));
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        var attempt = jobs.FirstRunner.TryRunAsync(id, TestContext.Current.CancellationToken);
        await script.Started;

        jobs.SecondClock.Advance(LeaseDuration(jobs));
        Assert.Equal(1, await jobs.SecondRunner.RecoverExpiredLeasesAsync(TestContext.Current.CancellationToken));
        jobs.FirstClock.Advance(RenewalInterval(jobs));
        var completed = await attempt;

        Assert.True(script.TokenFired);
        Assert.Null(completed!.RecordedStatus);
        Assert.Equal(JobRunStatus.Failed, (await jobs.RunAsync(id)).Status);
        Assert.Contains(JobsDatabase.LogsOf(jobs.First).GetSnapshot(), record => record.Level == LogLevel.Warning && record.Message.Contains("lost its lease", StringComparison.Ordinal));
    }

    private static TimeSpan LeaseDuration(JobsDatabase jobs) => jobs.First.Services.GetRequiredService<IOptions<JobRunnerOptions>>().Value.LeaseDuration;

    private static TimeSpan RenewalInterval(JobsDatabase jobs) => jobs.First.Services.GetRequiredService<IOptions<JobRunnerOptions>>().Value.RenewalInterval;
}
