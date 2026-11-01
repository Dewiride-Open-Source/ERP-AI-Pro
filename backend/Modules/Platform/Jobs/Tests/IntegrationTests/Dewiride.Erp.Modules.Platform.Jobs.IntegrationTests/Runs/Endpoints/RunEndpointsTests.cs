using System.Globalization;
using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Platform.Jobs.IntegrationTests.Runs.Endpoints.TestJobs;
using Dewiride.Erp.Modules.Platform.Jobs.Runs.Endpoints.Responses;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Jobs;

namespace Dewiride.Erp.Modules.Platform.Jobs.IntegrationTests.Runs.Endpoints;

public sealed class RunEndpointsTests(JobsHost host) : IClassFixture<JobsHost>
{
    [Fact]
    public async Task Get_Jobs_ListsEveryJobTheHostDefinesByNameWithItsAttempts()
    {
        using var client = host.Administrator();

        var jobs = await JobsApi.ListJobsAsync(client);

        var names = jobs.Select(job => job.Name).ToList();
        Assert.Equal(names.Order(StringComparer.Ordinal), names);
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        var succeeding = TestJobDefinitions.Succeeding;
        Assert.Equal(
            new JobResponse(succeeding.Name, succeeding.Title, succeeding.Description, succeeding.Retry.MaxAttempts, null),
            Assert.Single(jobs, job => job.Name == succeeding.Name));
    }

    [Fact]
    public async Task Get_JobsWithAValidSchedule_AnswersTheNormalisedExpressionAndTheNextOccurrenceWithoutAProblem()
    {
        using var client = host.Administrator();
        var before = host.Clock.GetUtcNow();

        var jobs = await JobsApi.ListJobsAsync(client);

        var after = host.Clock.GetUtcNow();
        var schedule = Assert.Single(jobs, job => job.Name == TestJobDefinitions.Scheduled.Name).Schedule;
        Assert.NotNull(schedule);
        Assert.Equal(TestJobDefinitions.NormalisedSchedule, schedule.Expression);
        Assert.Null(schedule.Problem);
        var next = Assert.NotNull(schedule.NextOccurrenceAt);
        Assert.InRange(next, before, after.AddDays(1));
        var inIndia = IndianStandardTime.ToIst(next);
        Assert.Equal((2, 15, 0), (inIndia.Hour, inIndia.Minute, inIndia.Second));
        Assert.Null(schedule.LastOccurrenceAt);
        Assert.Null(schedule.LastRunId);
        Assert.Null(schedule.LastRunStatus);
    }

    [Fact]
    public async Task Get_JobsWithAnInvalidSchedule_AnswersTheExpressionAsConfiguredWithTheProblemAndNoNextOccurrence()
    {
        using var client = host.Administrator();

        var jobs = await JobsApi.ListJobsAsync(client);

        Assert.Equal(
            new JobScheduleResponse(TestJobDefinitions.InvalidSchedule, null, "The minute field holds 61, but the minute allows 0 to 59.", null, null, null),
            Assert.Single(jobs, job => job.Name == TestJobDefinitions.Misscheduled.Name).Schedule);
    }

    [Fact]
    public async Task Get_Runs_AnswersThePagingEnvelopeNewestFirst()
    {
        using var client = host.Administrator();
        var marker = JobsApi.NewMarker();
        var first = await SeedAsync(TestJobDefinitions.Succeeding.Name, marker, JobRunStatus.Pending);
        var second = await SeedAsync(TestJobDefinitions.Succeeding.Name, marker, JobRunStatus.Pending);
        var third = await SeedAsync(TestJobDefinitions.Succeeding.Name, marker, JobRunStatus.Pending);

        using var response = await client.GetAsync(JobsApi.RunsPath($"?filter={JobsApi.Filter($"idempotencyKey:contains:{marker}")}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["items", "page", "pageSize", "totalCount", "totalPages"], body.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal((1, 50, 3L, 1), (body.RootElement.GetProperty("page").GetInt32(), body.RootElement.GetProperty("pageSize").GetInt32(), body.RootElement.GetProperty("totalCount").GetInt64(), body.RootElement.GetProperty("totalPages").GetInt32()));
        var items = body.RootElement.GetProperty("items");
        Assert.Equal(
            ["id", "jobName", "jobTitle", "idempotencyKey", "status", "dueAt", "createdAt", "requestedBy", "attemptCount", "failureCount", "maxAttempts", "startedAt", "finishedAt", "cancelRequestedAt", "lastErrorCode", "lastErrorMessage"],
            items[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal(["kind", "id", "name"], items[0].GetProperty("requestedBy").EnumerateObject().Select(property => property.Name));
        Assert.Equal("pending", items[0].GetProperty("status").GetString());
        Assert.Equal([third, second, first], items.Deserialize<List<JobRunResponse>>(JobsApi.Json)?.Select(run => run.Id));
    }

    [Fact]
    public async Task Get_RunsSortedByEachSortableField_AnswersThemInThatOrder()
    {
        using var client = host.Administrator();
        var marker = JobsApi.NewMarker();
        var first = await SeedAsync(TestJobDefinitions.Succeeding.Name, marker, JobRunStatus.DeadLettered);
        var second = await SeedAsync(TestJobDefinitions.Succeeding.Name, marker, JobRunStatus.DeadLettered);

        Assert.Equal([second, first], await IdsAsync(client, marker));
        Assert.Equal([first, second], await IdsAsync(client, marker, moreQuery: "&sort=createdAt:asc"));
        Assert.Equal([second, first], await IdsAsync(client, marker, moreQuery: "&sort=dueAt:desc"));
        Assert.Equal([first, second], await IdsAsync(client, marker, moreQuery: "&sort=finishedAt:asc"));
    }

    [Fact]
    public async Task Get_RunsSecondPageOfTwo_AnswersTheOldestRun()
    {
        using var client = host.Administrator();
        var marker = JobsApi.NewMarker();
        var first = await SeedAsync(TestJobDefinitions.Succeeding.Name, marker, JobRunStatus.Pending);
        await SeedAsync(TestJobDefinitions.Succeeding.Name, marker, JobRunStatus.Pending);
        await SeedAsync(TestJobDefinitions.Succeeding.Name, marker, JobRunStatus.Pending);

        var page = await JobsApi.ListMarkedAsync(client, marker, moreQuery: "&page=2&pageSize=2");

        Assert.Equal(first, Assert.Single(page.Items).Id);
        Assert.Equal((2, 2, 3L, 2), (page.Page, page.PageSize, page.TotalCount, page.TotalPages));
    }

    [Fact]
    public async Task Get_RunsFilteredByEachField_SelectsTheRunsItNames()
    {
        using var client = host.Administrator();
        var marker = JobsApi.NewMarker();
        var waiting = await SeedAsync(TestJobDefinitions.Succeeding.Name, marker, JobRunStatus.Pending);
        var gaveUp = await SeedAsync(TestJobDefinitions.Succeeding.Name, marker, JobRunStatus.DeadLettered);
        var retired = await SeedAsync(TestJobDefinitions.RetiredJobName, marker, JobRunStatus.Succeeded);
        var created = (await JobsApi.ListMarkedAsync(client, marker)).Items.Single(run => run.Id == gaveUp).CreatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        Assert.Equal([retired], await IdsAsync(client, marker, $";jobName:eq:{TestJobDefinitions.RetiredJobName}"));
        Assert.Equal(Sorted([waiting, gaveUp]), Sorted(await IdsAsync(client, marker, $";jobName:in:{TestJobDefinitions.Succeeding.Name}|{TestJobDefinitions.Scheduled.Name}")));
        Assert.Equal([retired], await IdsAsync(client, marker, $";jobName:ne:{TestJobDefinitions.Succeeding.Name}"));
        Assert.Equal(Sorted([waiting, retired]), Sorted(await IdsAsync(client, marker, ";status:ne:deadLettered")));
        Assert.Equal([gaveUp], await IdsAsync(client, marker, ";status:eq:deadLettered"));
        Assert.Equal(Sorted([waiting, retired]), Sorted(await IdsAsync(client, marker, ";status:in:pending|succeeded")));
        Assert.Equal([gaveUp], await IdsAsync(client, marker, $";createdAt:gte:{created};createdAt:lte:{created}"));
        Assert.Equal(Sorted([gaveUp, retired]), Sorted(await IdsAsync(client, marker, $";finishedAt:gte:{created}")));
        Assert.Empty(await IdsAsync(client, $"{marker}-none"));
    }

    [Theory]
    [InlineData("sort=jobName:asc", "query.invalid-field")]
    [InlineData("sort=createdAt:sideways", "query.invalid-sort")]
    [InlineData("filter=payload:eq:x", "query.invalid-field")]
    [InlineData("filter=jobName:contains:test", "query.invalid-operator")]
    [InlineData("filter=status:gt:pending", "query.invalid-operator")]
    [InlineData("filter=status:eq:waiting", "query.invalid-value")]
    [InlineData("filter=createdAt:gt:yesterday", "query.invalid-value")]
    [InlineData("filter=status", "query.invalid-filter")]
    [InlineData("page=0", "query.invalid-page")]
    [InlineData("pageSize=201", "query.invalid-page")]
    public async Task Get_RunsQueryOutsideTheAllowLists_AnswersTheQueryErrorCode(string query, string code)
    {
        using var client = host.Administrator();

        using var response = await client.GetAsync(JobsApi.RunsPath($"?{query}"), TestContext.Current.CancellationToken);

        await JobsApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task Get_RunThatGaveUp_AnswersItWithEveryStepOfItsHistoryOldestFirst()
    {
        using var client = host.Administrator();
        var key = $"history:{JobsApi.NewMarker()}";
        var id = await JobsApi.SeedAsync(host.Factory.Services, new JobRunSeed(TestJobDefinitions.Succeeding.Name, key, JobRunStatus.DeadLettered) { Attempts = 2 });

        var history = await JobsApi.GetRunAsync(client, id);

        var run = history.Run;
        var succeeding = TestJobDefinitions.Succeeding;
        Assert.Equal((id, succeeding.Name, succeeding.Title, key), (run.Id, run.JobName, run.JobTitle, run.IdempotencyKey));
        Assert.Equal((JobRunStatus.DeadLettered, 2, 2, succeeding.Retry.MaxAttempts), (run.Status, run.AttemptCount, run.FailureCount, run.MaxAttempts));
        Assert.Equal(new JobActorResponse(JobActorKind.System, ActorIds.System, null), run.RequestedBy);
        Assert.Equal((JobRunSeed.DefaultErrorCode, JobRunSeed.DefaultErrorMessage), (run.LastErrorCode, run.LastErrorMessage));
        Assert.NotNull(run.StartedAt);
        Assert.NotNull(run.FinishedAt);
        Assert.Null(run.CancelRequestedAt);
        var events = history.Events;
        Assert.Equal([JobRunEventKind.Enqueued, JobRunEventKind.Started, JobRunEventKind.Failed, JobRunEventKind.Started, JobRunEventKind.DeadLettered], events.Select(step => step.Kind));
        Assert.Equal([null, 1, 1, 2, 2], events.Select(step => step.Attempt));
        Assert.Equal(events.OrderBy(step => step.OccurredAt), events);
        Assert.All(events, step => Assert.Equal(new JobActorResponse(JobActorKind.System, ActorIds.System, null), step.Actor));
        Assert.Null(events[0].Runner);
        Assert.Null(events[0].TraceId);
        Assert.All(events.Skip(1), step => Assert.Matches("^[0-9a-f]{32}$", step.TraceId));
        Assert.All(events.Skip(1), step => Assert.False(string.IsNullOrEmpty(step.Runner)));
        Assert.Equal(events[1].TraceId, events[2].TraceId);
        Assert.NotEqual(events[2].TraceId, events[3].TraceId);
        Assert.Equal([null, null, JobRunSeed.DefaultErrorCode, null, JobRunSeed.DefaultErrorCode], events.Select(step => step.ErrorCode));
        Assert.Equal([null, null, JobRunSeed.DefaultErrorMessage, null, JobRunSeed.DefaultErrorMessage], events.Select(step => step.ErrorMessage));
        Assert.Equal(events[3].OccurredAt, events[2].RetryAt);
        Assert.Null(events[4].RetryAt);
    }

    [Fact]
    public async Task Get_RunOfAJobNoHostDefines_AnswersItWithoutATitleOrAnAttemptLimit()
    {
        using var client = host.Administrator();
        var id = await SeedAsync(TestJobDefinitions.RetiredJobName, JobsApi.NewMarker(), JobRunStatus.Succeeded);

        var run = (await JobsApi.GetRunAsync(client, id)).Run;

        Assert.Equal((TestJobDefinitions.RetiredJobName, JobRunStatus.Succeeded), (run.JobName, run.Status));
        Assert.Null(run.JobTitle);
        Assert.Null(run.MaxAttempts);
    }

    [Fact]
    public async Task Get_RunEnqueuedByAPerson_NamesThemByTheNameTheirRecordHasToday()
    {
        var person = host.AdmitAdministrator("Ritu Bansal");
        using var session = TestSignIn.CreateClient(host.Factory);
        using var signIn = await TestSignIn.SignInAsync(session, person);
        using var client = host.Administrator();
        var marker = JobsApi.NewMarker();
        var id = await JobsApi.SeedAsync(host.Factory.Services, new JobRunSeed(TestJobDefinitions.Succeeding.Name, $"requested:{marker}", JobRunStatus.Pending) { RequestedBy = person.ObjectId });

        var expected = new JobActorResponse(JobActorKind.Person, person.ObjectId, person.Name);
        var history = await JobsApi.GetRunAsync(client, id);
        Assert.Equal(expected, history.Run.RequestedBy);
        Assert.Equal(expected, Assert.Single(history.Events).Actor);
        Assert.Equal(expected, Assert.Single((await JobsApi.ListMarkedAsync(client, marker)).Items).RequestedBy);
    }

    [Fact]
    public async Task Get_UnknownRun_AnswersRunNotFound()
    {
        using var client = host.Administrator();

        using var response = await client.GetAsync(JobsApi.RunPath(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        await JobsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, JobErrors.RunNotFound.Code);
    }

    private static async Task<IReadOnlyList<Guid>> IdsAsync(HttpClient client, string marker, string moreTerms = "", string moreQuery = "") =>
        [.. (await JobsApi.ListMarkedAsync(client, marker, moreTerms, moreQuery)).Items.Select(run => run.Id)];

    private static List<Guid> Sorted(IEnumerable<Guid> ids) => [.. ids.Order()];

    private Task<Guid> SeedAsync(string jobName, string marker, JobRunStatus status) =>
        JobsApi.SeedAsync(host.Factory.Services, new JobRunSeed(jobName, $"{status}:{marker}:{Guid.CreateVersion7():N}", status));
}
