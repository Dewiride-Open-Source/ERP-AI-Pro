using System.Globalization;
using System.Net;
using System.Text.Json;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.EndToEnd;
using Dewiride.Erp.Testing.EndToEnd.Jobs;

namespace Dewiride.Erp.Host.Api.IntegrationTests.EndToEnd;

// Each test job has one schedule row in the test process's database and the first seed of its last run stays, so one test alone
// seeds each job, and the tests that are refused seed nothing.
public sealed class JobSchedulesTests(JobSchedulesTests.Fixture fixture) : IClassFixture<JobSchedulesTests.Fixture>
{
    private const string JobsRoute = "/api/platform/jobs";

    private static readonly DateTimeOffset LastNight = DateTimeOffset.Parse("2026-11-01T02:15:00+05:30", CultureInfo.InvariantCulture);

    [Fact]
    public async Task Post_ScheduleOfTheScheduledJobNamingASeededRun_MakesItTheJobsLastRunAndKeepsTheFirstSeed()
    {
        using var anonymous = fixture.Factory.CreateClient();
        var runId = await SeedRunAsync(anonymous, EndToEndJobs.Scheduled.Name);

        var seeded = await SeedScheduleAsync(anonymous, new { jobName = EndToEndJobs.Scheduled.Name, lastRunId = runId, lastOccurrenceAt = LastNight });
        var repeated = await SeedScheduleAsync(anonymous, new { jobName = EndToEndJobs.Scheduled.Name, lastRunId = Guid.CreateVersion7(), lastOccurrenceAt = LastNight.AddDays(1) });

        Assert.Equal(new JobScheduleSeedResponse(EndToEndJobs.Scheduled.Name, runId, LastNight), seeded);
        Assert.Equal(seeded, repeated);
        using var jobs = await ListJobsAsync();
        var job = JobNamed(jobs, EndToEndJobs.Scheduled.Name);
        Assert.Equal((EndToEndJobs.Scheduled.Title, EndToEndJobs.Scheduled.Description), (job.GetProperty("title").GetString(), job.GetProperty("description").GetString()));
        var schedule = job.GetProperty("schedule");
        Assert.Equal((EndToEndJobs.Schedule, JsonValueKind.Null), (schedule.GetProperty("expression").GetString(), schedule.GetProperty("problem").ValueKind));
        Assert.Equal((LastNight, runId, "succeeded"), (schedule.GetProperty("lastOccurrenceAt").GetDateTimeOffset(), schedule.GetProperty("lastRunId").GetGuid(), schedule.GetProperty("lastRunStatus").GetString()));
        Assert.Equal(new TimeOnly(20, 45), TimeOnly.FromDateTime(schedule.GetProperty("nextOccurrenceAt").GetDateTimeOffset().UtcDateTime));
    }

    [Fact]
    public async Task Post_ScheduleOfTheMisscheduledJobNamingNoRun_ShowsTheLastOccurrenceWithoutAStatusBesideTheProblem()
    {
        using var anonymous = fixture.Factory.CreateClient();
        var gone = Guid.CreateVersion7();

        var seeded = await SeedScheduleAsync(anonymous, new { jobName = EndToEndJobs.Misscheduled.Name, lastRunId = gone, lastOccurrenceAt = LastNight });

        Assert.Equal(new JobScheduleSeedResponse(EndToEndJobs.Misscheduled.Name, gone, LastNight), seeded);
        using var jobs = await ListJobsAsync();
        var schedule = JobNamed(jobs, EndToEndJobs.Misscheduled.Name).GetProperty("schedule");
        Assert.Equal(
            (EndToEndJobs.InvalidSchedule, JsonValueKind.Null, "The minute field holds 61, but the minute allows 0 to 59."),
            (schedule.GetProperty("expression").GetString(), schedule.GetProperty("nextOccurrenceAt").ValueKind, schedule.GetProperty("problem").GetString()));
        Assert.Equal((LastNight, gone, JsonValueKind.Null), (schedule.GetProperty("lastOccurrenceAt").GetDateTimeOffset(), schedule.GetProperty("lastRunId").GetGuid(), schedule.GetProperty("lastRunStatus").ValueKind));
    }

    [Theory]
    [InlineData("identity.users.directory-account-check")]
    [InlineData("test.jobs.end-to-end-seed")]
    public async Task Post_ScheduleOfAJobOtherThanTheTestJobs_Answers400(string jobName)
    {
        using var anonymous = fixture.Factory.CreateClient();

        using var response = await anonymous.PostAsJsonAsync(new Uri(JobSchedules.Path, UriKind.Relative), new { jobName, lastRunId = Guid.CreateVersion7(), lastOccurrenceAt = LastNight }, TestContext.Current.CancellationToken);

        Assert.Equal(["jobName"], await ErrorKeysAsync(response));
    }

    [Fact]
    public async Task Post_ScheduleWithoutALastRunOrItsTime_Answers400ForEach()
    {
        using var anonymous = fixture.Factory.CreateClient();

        using var response = await anonymous.PostAsJsonAsync(new Uri(JobSchedules.Path, UriKind.Relative), new { jobName = EndToEndJobs.Scheduled.Name, lastRunId = Guid.Empty }, TestContext.Current.CancellationToken);

        Assert.Equal(["lastOccurrenceAt", "lastRunId"], await ErrorKeysAsync(response));
    }

    [Fact]
    public async Task Post_ScheduleNamingARunOfAnotherJob_Answers400()
    {
        using var anonymous = fixture.Factory.CreateClient();
        var runOfTheOtherJob = await SeedRunAsync(anonymous, EndToEndJobs.Misscheduled.Name);

        using var response = await anonymous.PostAsJsonAsync(
            new Uri(JobSchedules.Path, UriKind.Relative),
            new { jobName = EndToEndJobs.Scheduled.Name, lastRunId = runOfTheOtherJob, lastOccurrenceAt = LastNight },
            TestContext.Current.CancellationToken);

        Assert.Equal(["lastRunId"], await ErrorKeysAsync(response));
    }

    [Fact]
    public async Task Map_InTheDevelopmentEnvironment_StopsTheHostFromStarting()
    {
        await using var factory = new ErpApiFactory().WithTestEndpoints(JobSchedules.Map);

        var exception = Assert.IsType<InvalidOperationException>(factory.StartFailure());

        Assert.Contains(EndToEndHost.EnvironmentName, exception.Message, StringComparison.Ordinal);
    }

    private static async Task<Guid> SeedRunAsync(HttpClient anonymous, string jobName)
    {
        using var seeded = await anonymous.PostAsJsonAsync(
            new Uri(JobRuns.Path, UriKind.Relative),
            new { jobName, idempotencyKey = $"seed:{Guid.CreateVersion7():N}", status = "succeeded" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);

        return (await seeded.Content.ReadFromJsonAsync<JobRunSeedResponse>(TestContext.Current.CancellationToken))!.Id;
    }

    private static async Task<JobScheduleSeedResponse> SeedScheduleAsync(HttpClient anonymous, object seed)
    {
        using var response = await anonymous.PostAsJsonAsync(new Uri(JobSchedules.Path, UriKind.Relative), seed, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["jobName", "lastRunId", "lastOccurrenceAt"], body.RootElement.EnumerateObject().Select(member => member.Name));

        return body.RootElement.Deserialize<JobScheduleSeedResponse>(JsonSerializerOptions.Web)!;
    }

    private static async Task<IReadOnlyList<string>> ErrorKeysAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);

        return [.. problem!.Errors.Keys.Order(StringComparer.Ordinal)];
    }

    private static JsonElement JobNamed(JsonDocument jobs, string name) =>
        Assert.Single(jobs.RootElement.EnumerateArray(), job => job.GetProperty("name").GetString() == name);

    private async Task<JsonDocument> ListJobsAsync()
    {
        using var administrator = fixture.Factory.CreateClient().AsUser(TestUsers.Administrator);
        using var response = await administrator.GetAsync(new Uri(JobsRoute, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = ErpApiFactory.ForEnvironment(EndToEndHost.EnvironmentName)
            .WithTestServices(EndToEndJobs.Register)
            .WithTestEndpoints(JobRuns.Map)
            .WithTestEndpoints(JobSchedules.Map);

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
