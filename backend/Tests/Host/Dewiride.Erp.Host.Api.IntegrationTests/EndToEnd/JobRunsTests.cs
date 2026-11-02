using System.Net;
using System.Text.Json;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.EndToEnd;
using Dewiride.Erp.Testing.EndToEnd.Jobs;

namespace Dewiride.Erp.Host.Api.IntegrationTests.EndToEnd;

public sealed class JobRunsTests(JobRunsTests.Fixture fixture) : IClassFixture<JobRunsTests.Fixture>
{
    private const string JobName = "test.jobs.end-to-end-seed";

    private const string RunsRoute = "/api/platform/jobs/runs";

    [Fact]
    public async Task Post_SeedOfARunThatGaveUp_SeedsTheRunWithItsAttemptsAndErrorAsTheJobsApiAnswersIt()
    {
        using var anonymous = fixture.Factory.CreateClient();
        var key = NewKey();

        using var seeded = await anonymous.PostAsJsonAsync(
            new Uri(JobRuns.Path, UriKind.Relative),
            new { jobName = JobName, idempotencyKey = key, status = "deadLettered", attempts = 2, errorCode = "directory.unavailable", errorMessage = "The company directory cannot answer now." },
            TestContext.Current.CancellationToken);

        var id = await IdOfAsync(seeded);
        using var run = await RunAsync(id);
        var details = run.RootElement.GetProperty("run");
        Assert.Equal(id, details.GetProperty("id").GetGuid());
        Assert.Equal((JobName, key, "deadLettered"), (details.GetProperty("jobName").GetString(), details.GetProperty("idempotencyKey").GetString(), details.GetProperty("status").GetString()));
        Assert.Equal((2, 2), (details.GetProperty("attemptCount").GetInt32(), details.GetProperty("failureCount").GetInt32()));
        Assert.Equal(("directory.unavailable", "The company directory cannot answer now."), (details.GetProperty("lastErrorCode").GetString(), details.GetProperty("lastErrorMessage").GetString()));
        Assert.Equal("system", details.GetProperty("requestedBy").GetProperty("kind").GetString());
        Assert.Equal(
            ["enqueued", "started", "failed", "started", "deadLettered"],
            run.RootElement.GetProperty("events").EnumerateArray().Select(step => step.GetProperty("kind").GetString()));
    }

    [Fact]
    public async Task Post_SeedOfARunningRunAskedToStop_SeedsTheStopRequestOfWhoeverEnqueuedTheRun()
    {
        using var anonymous = fixture.Factory.CreateClient();
        var requestedBy = Guid.CreateVersion7();

        using var seeded = await anonymous.PostAsJsonAsync(
            new Uri(JobRuns.Path, UriKind.Relative),
            new { jobName = JobName, idempotencyKey = NewKey(), status = "running", stopRequested = true, requestedBy },
            TestContext.Current.CancellationToken);

        using var run = await RunAsync(await IdOfAsync(seeded));
        var details = run.RootElement.GetProperty("run");
        Assert.Equal("running", details.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, details.GetProperty("cancelRequestedAt").ValueKind);
        Assert.Equal(("person", requestedBy), (details.GetProperty("requestedBy").GetProperty("kind").GetString(), details.GetProperty("requestedBy").GetProperty("id").GetGuid()));
        var request = Assert.Single(run.RootElement.GetProperty("events").EnumerateArray(), step => step.GetProperty("kind").GetString() == "cancelRequested");
        Assert.Equal(requestedBy, request.GetProperty("actor").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Post_KnownSeed_AnswersTheRunIdInThisShape()
    {
        using var anonymous = fixture.Factory.CreateClient();

        using var seeded = await anonymous.PostAsJsonAsync(new Uri(JobRuns.Path, UriKind.Relative), new { jobName = JobName, idempotencyKey = NewKey(), status = "pending" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        using var body = JsonDocument.Parse(await seeded.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["id"], body.RootElement.EnumerateObject().Select(member => member.Name));
    }

    [Fact]
    public async Task Post_SeedWithAMalformedJobNameAndNoKeyOrStatus_Answers400ForEach()
    {
        using var anonymous = fixture.Factory.CreateClient();

        using var response = await anonymous.PostAsJsonAsync(new Uri(JobRuns.Path, UriKind.Relative), new { jobName = "Nightly check" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(["idempotencyKey", "jobName", "status"], problem!.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Post_SeedWhoseFieldsTheEngineCouldNotHaveStored_Answers400ForEach()
    {
        using var anonymous = fixture.Factory.CreateClient();

        using var response = await anonymous.PostAsJsonAsync(
            new Uri(JobRuns.Path, UriKind.Relative),
            new { jobName = JobName, idempotencyKey = NewKey(), status = "pending", attempts = 21, errorCode = "job failed", errorMessage = " ", stopRequested = true, requestedBy = Guid.Empty },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(["attempts", "errorCode", "errorMessage", "requestedBy", "stopRequested"], problem!.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Post_SeedWithAStatusNoRunHas_Answers400()
    {
        using var anonymous = fixture.Factory.CreateClient();

        using var response = await anonymous.PostAsJsonAsync(new Uri(JobRuns.Path, UriKind.Relative), new { jobName = JobName, idempotencyKey = NewKey(), status = "waiting" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Map_InTheDevelopmentEnvironment_StopsTheHostFromStarting()
    {
        await using var factory = new ErpApiFactory().WithTestEndpoints(JobRuns.Map);

        var exception = Assert.IsType<InvalidOperationException>(factory.StartFailure());

        Assert.Contains(EndToEndHost.EnvironmentName, exception.Message, StringComparison.Ordinal);
    }

    private static string NewKey() => $"seed:{Guid.CreateVersion7():N}";

    private static async Task<Guid> IdOfAsync(HttpResponseMessage seeded)
    {
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        var run = await seeded.Content.ReadFromJsonAsync<JobRunSeedResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(run);

        return run.Id;
    }

    private async Task<JsonDocument> RunAsync(Guid id)
    {
        using var administrator = fixture.Factory.CreateClient().AsUser(TestUsers.Administrator);
        using var response = await administrator.GetAsync(new Uri($"{RunsRoute}/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = ErpApiFactory.ForEnvironment(EndToEndHost.EnvironmentName).WithTestEndpoints(JobRuns.Map);

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
