using Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.Testing.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Running;

// A runner claims and recovers only the runs of the jobs it defines. The runs of a job no runner here defines are dated in 2000,
// before every other test's runs, so a runner that ignored the job names would reach them first, and only them, since its clock
// stops there.
public sealed class JobClaimTests
{
    private static readonly DateTimeOffset BeforeEveryOtherRun = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StartDueAsync_RunsOfAJobThisRunnerDoesNotDefine_ClaimsAndRecoversNeither()
    {
        var undefined = JobsDatabase.NewName("undefined");
        await using var jobs = JobsDatabase.Create(
            services => services.AddSingleton(JobScript.Succeeding()).AddJob<ScriptedJob>(JobsDatabase.Definition(JobsDatabase.NewName("defined"))),
            BeforeEveryOtherRun);
        var pending = await SeedAsync(jobs, new JobRunSeed(undefined, JobsDatabase.NewKey(), JobRunStatus.Pending));
        var running = await SeedAsync(jobs, new JobRunSeed(undefined, JobsDatabase.NewKey(), JobRunStatus.Running));
        jobs.Advance(JobRunSeeder.RunningLease + TimeSpan.FromMinutes(1));
        var before = await StatesAsync(jobs, pending, running);

        var tried = await jobs.FirstRunner.TryRunAsync(pending, TestContext.Current.CancellationToken);
        var started = await jobs.FirstRunner.StartDueAsync(TestContext.Current.CancellationToken);
        var recovered = await jobs.FirstRunner.RecoverExpiredLeasesAsync(TestContext.Current.CancellationToken);

        Assert.Null(tried);
        Assert.Equal((0, 0), (started, recovered));
        Assert.Equal(before, await StatesAsync(jobs, pending, running));
        Assert.Equal([(JobRunStatus.Pending, 0), (JobRunStatus.Running, 1)], before.Select(state => (state.Status, state.AttemptCount)));
    }

    [Fact]
    public async Task StartDueAsync_ShutdownBeginsAsTheClaimCommits_RunsTheAttemptForTheShutdownToInterrupt()
    {
        var script = JobScript.Succeeding().WaitingForItsToken();
        await using var jobs = JobsDatabase.Create(services => JobsTransactionHook.Register(services.AddSingleton(script).AddJob<ScriptedJob>(JobsDatabase.Definition(JobsDatabase.NewName("claim-at-shutdown")))));
        var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
        using var shutdown = new CancellationTokenSource();
        JobsTransactionHook.Of(jobs.First).AfterNextCommit(() => shutdown.CancelAsync());

        var started = await jobs.FirstRunner.StartDueAsync(shutdown.Token);
        await script.Started.WaitAsync(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        jobs.FirstRunner.Abort();
        await jobs.FirstRunner.WhenIdleAsync();

        Assert.Equal(1, started);
        Assert.True(shutdown.IsCancellationRequested);
        Assert.True(script.TokenFired);
        var run = await jobs.RunAsync(id);
        Assert.Equal((JobRunStatus.Pending, 0, 1, (Guid?)null), (run.Status, run.FailureCount, run.AttemptCount, run.LeaseToken));
        Assert.Equal(jobs.FirstClock.GetUtcNow(), run.DueAt);
        Assert.Equal(1, Assert.Single(await jobs.EventsAsync(id), e => e.Kind == JobRunEventKind.Interrupted).Attempt);
    }

    private static Task<JobRunId> SeedAsync(JobsDatabase jobs, JobRunSeed seed) =>
        JobRunSeeder.SeedAsync(jobs.First.Services, seed, TestContext.Current.CancellationToken);

    private static Task<List<(JobRunStatus Status, int AttemptCount, DateTimeOffset DueAt, Guid? LeaseToken, DateTimeOffset? LeaseExpiresAt)>> StatesAsync(JobsDatabase jobs, JobRunId pending, JobRunId running) =>
        jobs.QueryAsync(async (context, cancellationToken) =>
        {
            var runs = await context.Runs.AsNoTracking().Where(r => r.Id == pending || r.Id == running).ToListAsync(cancellationToken);

            return runs
                .OrderBy(run => run.Id == pending ? 0 : 1)
                .Select(run => (run.Status, run.AttemptCount, run.DueAt, run.LeaseToken, run.LeaseExpiresAt))
                .ToList();
        });
}
