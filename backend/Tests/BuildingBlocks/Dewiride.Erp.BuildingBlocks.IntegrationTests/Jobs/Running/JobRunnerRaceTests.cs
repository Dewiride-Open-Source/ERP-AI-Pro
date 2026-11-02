using System.Diagnostics;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Running;

// The claim is a compare-and-set on the attempt count each runner read: two runners that read the same due run both try to
// claim it, and SQL Server lets exactly one of them change the row.
public sealed class JobRunnerRaceTests
{
    private const int Rounds = 10;

    private const int RaceRuns = 50;

    [Fact]
    public async Task TryClaimAsync_TwoRunnersAtOnce_OneClaims()
    {
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(JobScript.Succeeding()).AddJob<ScriptedJob>(JobsDatabase.Definition(JobsDatabase.NewName("claim"))));

        for (var round = 0; round < Rounds; round++)
        {
            var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var claims = new[] { jobs.FirstRunner, jobs.SecondRunner }.Select(runner => Task.Run(async () =>
            {
                await gate.Task;
                return await runner.TryClaimAsync(id, 0, ActivityTraceId.CreateRandom().ToHexString(), TestContext.Current.CancellationToken);
            })).ToArray();

            gate.SetResult();
            var claimed = await Task.WhenAll(claims);

            var winner = Assert.Single(claimed, claim => claim is not null)!;
            var run = await jobs.RunAsync(id);
            Assert.Equal((JobRunStatus.Running, 1, winner.LeaseToken), (run.Status, run.AttemptCount, run.LeaseToken!.Value));
            Assert.Equal(winner.Runner, run.LeaseHolder);
            var started = Assert.Single(await jobs.EventsAsync(id), e => e.Kind == JobRunEventKind.Started);
            Assert.Equal((1, winner.TraceId), (started.Attempt!.Value, started.TraceId));
        }
    }

    [Fact]
    public async Task StartDueAsync_TwoRunnersRacingForFiftyRuns_RunEachOnce()
    {
        var name = JobsDatabase.NewName("race");
        var script = JobScript.Succeeding();
        var settings = new Dictionary<string, string?> { ["Erp:Platform:Jobs:MaxConcurrentRuns"] = "16" };
        await using var jobs = JobsDatabase.Create(services => services.AddSingleton(script).AddJob<ScriptedJob>(JobsDatabase.Definition(name)), settings: settings);
        var ids = new List<JobRunId>();
        for (var index = 0; index < RaceRuns; index++)
        {
            ids.Add(await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey()));
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        int started;
        do
        {
            var polls = await Task.WhenAll(Task.Run(() => jobs.FirstRunner.StartDueAsync(cancellationToken)), Task.Run(() => jobs.SecondRunner.StartDueAsync(cancellationToken)));
            await Task.WhenAll(jobs.FirstRunner.WhenIdleAsync(), jobs.SecondRunner.WhenIdleAsync());
            started = polls.Sum();
        }
        while (started > 0);

        Assert.Equal(ids.Select(id => id.Value).Order(), script.Executions.Select(execution => execution.RunId.Value).Order());
        var runs = await jobs.QueryAsync((context, token) => context.Runs.AsNoTracking().Where(r => r.JobName == name).ToListAsync(token));
        Assert.All(runs, run => Assert.Equal((JobRunStatus.Succeeded, 1), (run.Status, run.AttemptCount)));
        var startedEvents = await jobs.QueryAsync((context, token) => context.Events.AsNoTracking().Where(e => ids.Contains(e.RunId) && e.Kind == JobRunEventKind.Started).CountAsync(token));
        Assert.Equal(RaceRuns, startedEvents);
    }
}
