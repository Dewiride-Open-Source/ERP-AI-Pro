using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Scheduling;

// The clocks start at noon in India on Sunday 1 November 2026 and the job runs at 00:30 India time.
[Collection(JobScheduleHolders.Name)]
public sealed class JobSchedulerTests
{
    private static readonly DateTimeOffset FirstNight = Ist("2026-11-02T00:30");

    [Fact]
    public async Task EnqueueDueAsync_FirstSight_EnqueuesNoPastOccurrence()
    {
        var (name, _, jobs) = Create();
        await using (jobs)
        {
            Assert.Equal(0, await EnqueueAsync(jobs.First));

            var row = await ScheduleOf(jobs, name);
            Assert.Equal(("30 0 * * *", FirstNight, (DateTimeOffset?)null, (JobRunId?)null), (row.Expression, row.NextOccurrenceAt, row.LastOccurrenceAt, row.LastRunId));
            Assert.Empty(await RunsOf(jobs, name));
        }
    }

    [Fact]
    public async Task EnqueueDueAsync_DueOccurrence_EnqueuesItOnce()
    {
        var (name, _, jobs) = Create();
        await using (jobs)
        {
            await EnqueueAsync(jobs.First);
            MoveTo(jobs, Ist("2026-11-02T00:31"));

            Assert.Equal(1, await EnqueueAsync(jobs.First));
            Assert.Equal(0, await EnqueueAsync(jobs.First));

            var run = Assert.Single(await RunsOf(jobs, name));
            Assert.Equal(("occurrence:2026-11-02T00:30+05:30", FirstNight, ActorIds.System, JobRunStatus.Pending), (run.IdempotencyKey, run.DueAt, run.RequestedBy, run.Status));
            Assert.Equal(ActorIds.System, Assert.Single(await jobs.EventsAsync(run.Id)).ActorId);
            var row = await ScheduleOf(jobs, name);
            Assert.Equal((Ist("2026-11-03T00:30"), FirstNight, run.Id), (row.NextOccurrenceAt, row.LastOccurrenceAt!.Value, row.LastRunId!.Value));
        }
    }

    [Fact]
    public async Task EnqueueDueAsync_TwoSchedulersAtOnce_EnqueueOneRun()
    {
        var (name, _, jobs) = Create();
        await using (jobs)
        {
            await EnqueueAsync(jobs.First);
            MoveTo(jobs, Ist("2026-11-02T00:31"));
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var polls = new[] { jobs.First, jobs.Second }.Select(host => Task.Run(async () =>
            {
                await gate.Task;
                return await EnqueueAsync(host);
            })).ToArray();

            gate.SetResult();

            Assert.Equal(1, (await Task.WhenAll(polls)).Sum());
            Assert.Single(await RunsOf(jobs, name));
        }
    }

    [Fact]
    public async Task EnqueueDueAsync_AfterARestart_DoesNotEnqueueTheOccurrenceAgain()
    {
        var (name, _, jobs) = Create();
        await using (jobs)
        {
            await EnqueueAsync(jobs.First);
            MoveTo(jobs, Ist("2026-11-02T00:31"));
            Assert.Equal(1, await EnqueueAsync(jobs.First));

            Assert.Equal(0, await EnqueueAsync(jobs.Second));

            Assert.Single(await RunsOf(jobs, name));
        }
    }

    [Fact]
    public async Task EnqueueDueAsync_ThreeDaysDown_EnqueuesOnlyTheLatestOccurrence()
    {
        var (name, _, jobs) = Create();
        await using (jobs)
        {
            await EnqueueAsync(jobs.First);
            MoveTo(jobs, Ist("2026-11-04T12:00"));

            Assert.Equal(1, await EnqueueAsync(jobs.First));

            var run = Assert.Single(await RunsOf(jobs, name));
            Assert.Equal(("occurrence:2026-11-04T00:30+05:30", Ist("2026-11-04T00:30")), (run.IdempotencyKey, run.DueAt));
            Assert.Equal(Ist("2026-11-05T00:30"), (await ScheduleOf(jobs, name)).NextOccurrenceAt);
        }
    }

    [Fact]
    public async Task EnqueueDueAsync_ExpressionChanged_RecomputesWithoutARun()
    {
        var (name, options, jobs) = Create();
        await using (jobs)
        {
            await EnqueueAsync(jobs.First);
            options.Expression = "0 2 * * *";
            MoveTo(jobs, Ist("2026-11-02T00:31"));

            Assert.Equal(0, await EnqueueAsync(jobs.First));
            var row = await ScheduleOf(jobs, name);
            Assert.Equal(("0 2 * * *", Ist("2026-11-02T02:00")), (row.Expression, row.NextOccurrenceAt));
            Assert.Empty(await RunsOf(jobs, name));

            MoveTo(jobs, Ist("2026-11-02T02:00"));
            Assert.Equal(1, await EnqueueAsync(jobs.First));
            Assert.Equal("occurrence:2026-11-02T02:00+05:30", Assert.Single(await RunsOf(jobs, name)).IdempotencyKey);
        }
    }

    [Fact]
    public async Task EnqueueDueAsync_PreviousRunUnfinished_SkipsTheOccurrence()
    {
        var (name, _, jobs) = Create();
        await using (jobs)
        {
            await EnqueueAsync(jobs.First);
            MoveTo(jobs, Ist("2026-11-02T00:31"));
            await EnqueueAsync(jobs.First);
            var first = Assert.Single(await RunsOf(jobs, name));
            MoveTo(jobs, Ist("2026-11-03T00:31"));

            Assert.Equal(0, await EnqueueAsync(jobs.First));

            Assert.Single(await RunsOf(jobs, name));
            var row = await ScheduleOf(jobs, name);
            Assert.Equal((Ist("2026-11-04T00:30"), first.Id, FirstNight), (row.NextOccurrenceAt, row.LastRunId!.Value, row.LastOccurrenceAt!.Value));
            Assert.Contains(JobsDatabase.LogsOf(jobs.First).GetSnapshot(), record => record.Message.StartsWith("Skipped the occurrence", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task EnqueueDueAsync_InvalidExpression_LogsOnceAndLeavesTheScheduleAlone()
    {
        var (name, options, jobs) = Create();
        await using (jobs)
        {
            await EnqueueAsync(jobs.First);
            var before = await ScheduleOf(jobs, name);
            options.Expression = "61 0 * * *";

            await EnqueueAsync(jobs.First);
            await EnqueueAsync(jobs.First);
            options.Expression = "a * * * *";
            await EnqueueAsync(jobs.First);
            options.Unreadable = true;
            await EnqueueAsync(jobs.First);
            await EnqueueAsync(jobs.First);

            var after = await ScheduleOf(jobs, name);
            Assert.Equal((before.Expression, before.NextOccurrenceAt), (after.Expression, after.NextOccurrenceAt));
            var records = JobsDatabase.LogsOf(jobs.First).GetSnapshot();
            Assert.Equal(["61 0 * * *", "a * * * *"], records.Where(record => record.Level == LogLevel.Error).Select(record => record.GetStructuredStateValue("Expression")));
            Assert.Single(records, record => record.Level == LogLevel.Warning && record.Message.Contains("could not be read", StringComparison.Ordinal));
            options.Unreadable = false;
            options.Expression = "30 0 * * *";
            MoveTo(jobs, Ist("2026-11-02T00:31"));
            Assert.Equal(1, await EnqueueAsync(jobs.First));
        }
    }

    private static (string Name, TestScheduleOptions Options, JobsDatabase Jobs) Create()
    {
        var name = JobsDatabase.NewName("schedule");
        var options = new TestScheduleOptions();
        var jobs = JobsDatabase.Create(services => services
            .AddSingleton(JobScript.Succeeding())
            .AddSingleton<IOptionsMonitor<TestScheduleOptions>>(options)
            .AddRecurringJob<ScriptedJob, TestScheduleOptions>(JobsDatabase.Definition(name), schedule => schedule.Expression));

        return (name, options, jobs);
    }

    private static DateTimeOffset Ist(string minute) => DateTimeOffset.Parse($"{minute}:00+05:30", CultureInfo.InvariantCulture);

    private static void MoveTo(JobsDatabase jobs, DateTimeOffset at)
    {
        jobs.FirstClock.SetUtcNow(at);
        jobs.SecondClock.SetUtcNow(at);
    }

    private static Task<int> EnqueueAsync(IHost host) => host.Services.GetRequiredService<JobScheduler>().EnqueueDueAsync(TestContext.Current.CancellationToken);

    private static Task<JobSchedule> ScheduleOf(JobsDatabase jobs, string name) =>
        jobs.QueryAsync((context, cancellationToken) => context.Schedules.AsNoTracking().SingleAsync(s => s.JobName == name, cancellationToken));

    private static Task<List<JobRun>> RunsOf(JobsDatabase jobs, string name) =>
        jobs.QueryAsync((context, cancellationToken) => context.Runs.AsNoTracking().Where(r => r.JobName == name).ToListAsync(cancellationToken));
}
