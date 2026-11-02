using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Testing.Jobs;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Administration;

[Collection(JobScheduleHolders.Name)]
public sealed class JobAdministrationTests
{
    private static readonly Guid Person = new("0198f6a2-7c1e-7b3a-9d4e-000000000b01");

    [Fact]
    public async Task ListRunsAsync_FilteredByJobAndStatus_ListsThoseRunsNewestFirstWithTheirRequesterNamed()
    {
        var (name, _, jobs) = Create();
        await using (jobs)
        {
            JobsDatabase.ActorOf(jobs.First).ActorId = Person;
            var older = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
            jobs.Advance(TimeSpan.FromSeconds(1));
            var newer = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());

            var page = await AdministerAsync(jobs, administration => administration.ListRunsAsync(
                new ListRequest(PageRequest.First, SortRequest.None, new FilterRequest([new FilterTerm("jobName", FilterOperator.Equal, [name]), new FilterTerm("status", FilterOperator.Equal, ["pending"])])),
                TestContext.Current.CancellationToken));

            Assert.Equal([newer, older], page.Value.Items.Select(run => run.Id));
            var run = page.Value.Items[0];
            Assert.Equal(new JobActorDetails(JobActorKind.Person, Person, "Asha Rao"), run.RequestedBy);
            Assert.Equal(("Test job", 5), (run.JobTitle, run.MaxAttempts!.Value));
            Assert.Equal(2, page.Value.TotalCount);
        }
    }

    [Fact]
    public async Task ListRunsAsync_KeyContainsAText_ListsOnlyThoseRuns()
    {
        var (name, _, jobs) = Create();
        await using (jobs)
        {
            var marker = Guid.CreateVersion7().ToString("N");
            var marked = await jobs.EnqueueAsync<ScriptedJob>($"marked:{marker}");
            await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());

            var page = await AdministerAsync(jobs, administration => administration.ListRunsAsync(
                new ListRequest(PageRequest.First, SortRequest.None, new FilterRequest([new FilterTerm("idempotencyKey", FilterOperator.Contains, [marker])])),
                TestContext.Current.CancellationToken));

            Assert.Equal(marked, Assert.Single(page.Value.Items).Id);
        }
    }

    [Theory]
    [InlineData("payload")]
    [InlineData("requestedBy")]
    public async Task ListRunsAsync_FieldOutsideTheAllowList_IsRefused(string field)
    {
        var (_, _, jobs) = Create();
        await using (jobs)
        {
            var refused = await AdministerAsync(jobs, administration => administration.ListRunsAsync(
                new ListRequest(PageRequest.First, SortRequest.None, new FilterRequest([new FilterTerm(field, FilterOperator.Equal, ["x"])])),
                TestContext.Current.CancellationToken));

            Assert.Equal(QueryErrors.InvalidField, refused.Error!.Code);
        }
    }

    [Fact]
    public async Task GetRunAsync_Run_ListsItsEventsInOrderWithTheirActorsNamed()
    {
        var (_, _, jobs) = Create();
        await using (jobs)
        {
            JobsDatabase.ActorOf(jobs.Second).ActorId = Person;
            var id = await jobs.EnqueueAsync<ScriptedJob>(JobsDatabase.NewKey());
            jobs.Advance(TimeSpan.FromSeconds(1));
            await AdministerAsync(jobs, administration => administration.CancelAsync(id, TestContext.Current.CancellationToken));

            var history = await AdministerAsync(jobs, administration => administration.GetRunAsync(id, TestContext.Current.CancellationToken));

            Assert.Equal(JobRunStatus.Cancelled, history.Value.Run.Status);
            Assert.Equal([JobRunEventKind.Enqueued, JobRunEventKind.Cancelled], history.Value.Events.Select(e => e.Kind));
            Assert.Equal(new JobActorDetails(JobActorKind.System, ActorIds.System, null), history.Value.Events[0].Actor);
            Assert.Equal(new JobActorDetails(JobActorKind.Person, Person, "Asha Rao"), history.Value.Events[1].Actor);
        }
    }

    [Fact]
    public async Task GetRunAsync_UnknownRun_IsNotFound()
    {
        var (_, _, jobs) = Create();
        await using (jobs)
        {
            var refused = await AdministerAsync(jobs, administration => administration.GetRunAsync(JobRunId.Create(), TestContext.Current.CancellationToken));

            Assert.Equal(JobErrors.RunNotFound, refused.Error);
        }
    }

    [Fact]
    public async Task ListJobsAsync_RecurringJob_ReportsItsScheduleAndItsLastRun()
    {
        var (name, _, jobs) = Create();
        await using (jobs)
        {
            var details = await ListJobsAsync(jobs);
            var job = Assert.Single(details, job => job.Name == name);
            Assert.Equal(new JobScheduleDetails("30 0 * * *", new DateTimeOffset(2026, 11, 1, 19, 0, 0, TimeSpan.Zero), null, null, null, null), job.Schedule);

            await jobs.Second.Services.GetRequiredService<JobScheduler>().EnqueueDueAsync(TestContext.Current.CancellationToken);
            jobs.Advance(TimeSpan.FromHours(13));
            await jobs.Second.Services.GetRequiredService<JobScheduler>().EnqueueDueAsync(TestContext.Current.CancellationToken);
            var after = Assert.Single(await ListJobsAsync(jobs), job => job.Name == name).Schedule!;

            Assert.Equal((new DateTimeOffset(2026, 11, 2, 19, 0, 0, TimeSpan.Zero), JobRunStatus.Pending), (after.NextOccurrenceAt!.Value, after.LastRunStatus!.Value));
            Assert.NotNull(after.LastRunId);
        }
    }

    [Fact]
    public async Task ListJobsAsync_InvalidSchedule_ReportsTheProblemAndNoNextOccurrence()
    {
        var (name, options, jobs) = Create();
        await using (jobs)
        {
            options.Expression = "0 0 30 2 *";

            var schedule = Assert.Single(await ListJobsAsync(jobs), job => job.Name == name).Schedule!;

            Assert.Equal(("0 0 30 2 *", (DateTimeOffset?)null), (schedule.Expression, schedule.NextOccurrenceAt));
            Assert.StartsWith("The schedule never runs", schedule.Problem, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task RerunAsync_RunOfAJobThisReleaseDoesNotDefine_IsRefused()
    {
        var (_, _, jobs) = Create();
        await using (jobs)
        {
            var id = await JobRunSeeder.SeedAsync(jobs.First.Services, new JobRunSeed(JobsDatabase.NewName("retired"), JobsDatabase.NewKey(), JobRunStatus.DeadLettered), TestContext.Current.CancellationToken);

            var refused = await AdministerAsync(jobs, administration => administration.RerunAsync(id, TestContext.Current.CancellationToken));
            var history = await AdministerAsync(jobs, administration => administration.GetRunAsync(id, TestContext.Current.CancellationToken));

            Assert.Equal(JobErrors.JobNotDefined, refused.Error);
            Assert.Equal(((string?)null, (int?)null), (history.Value.Run.JobTitle, history.Value.Run.MaxAttempts));
            Assert.Equal([JobRunEventKind.Enqueued, JobRunEventKind.Started, JobRunEventKind.DeadLettered], history.Value.Events.Select(e => e.Kind));
        }
    }

    private static (string Name, TestScheduleOptions Options, JobsDatabase Jobs) Create()
    {
        var name = JobsDatabase.NewName("administer");
        var options = new TestScheduleOptions();
        var jobs = JobsDatabase.Create(services => services
            .AddSingleton(JobScript.Succeeding())
            .AddSingleton<IOptionsMonitor<TestScheduleOptions>>(options)
            .AddScoped<IPersonNames>(_ => new StaticPersonNames(new Dictionary<Guid, string> { [Person] = "Asha Rao" }))
            .AddRecurringJob<ScriptedJob, TestScheduleOptions>(JobsDatabase.Definition(name), schedule => schedule.Expression));

        return (name, options, jobs);
    }

    private static async Task<IReadOnlyList<JobDetails>> ListJobsAsync(JobsDatabase jobs)
    {
        await using var scope = jobs.Second.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IJobAdministration>().ListJobsAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Result<T>> AdministerAsync<T>(JobsDatabase jobs, Func<IJobAdministration, Task<Result<T>>> action)
    {
        await using var scope = jobs.Second.Services.CreateAsyncScope();

        return await action(scope.ServiceProvider.GetRequiredService<IJobAdministration>());
    }
}
