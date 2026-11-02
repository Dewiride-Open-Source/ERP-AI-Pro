using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Modules.Platform.Jobs.Runs.Endpoints.Responses;
using Dewiride.Erp.Testing.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Modules.Platform.Jobs.IntegrationTests.Runs.Endpoints;

// Every host of the test process keeps its runs in the one test database, so a test lists only the runs whose idempotency keys
// carry a marker of its own.
internal static class JobsApi
{
    public const string Route = "/api/platform/jobs";

    public const string Rerun = "rerun";

    public const string Cancel = "cancel";

    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static Uri Path(string suffix) => new(Route + suffix, UriKind.Relative);

    public static Uri RunsPath(string query = "") => Path($"/runs{query}");

    public static Uri RunPath(Guid id, string? action = null) => Path(action is null ? $"/runs/{id}" : $"/runs/{id}/{action}");

    public static Uri RouteFor(string template) => Path(template.Replace("{id}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal));

    public static string NewMarker() => Guid.CreateVersion7().ToString("N");

    public static string Filter(string filter) => Uri.EscapeDataString(filter);

    public static async Task<Guid> SeedAsync(IServiceProvider services, JobRunSeed seed) =>
        (await JobRunSeeder.SeedAsync(services, seed, TestContext.Current.CancellationToken)).Value;

    public static async Task<IReadOnlyList<JobResponse>> ListJobsAsync(HttpClient client)
    {
        using var response = await client.GetAsync(Path(string.Empty), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jobs = await response.Content.ReadFromJsonAsync<List<JobResponse>>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(jobs);

        return jobs;
    }

    public static async Task<PagedResponse<JobRunResponse>> ListRunsAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync(RunsPath($"?{query}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<JobRunResponse>>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(page);

        return page;
    }

    public static Task<PagedResponse<JobRunResponse>> ListMarkedAsync(HttpClient client, string marker, string moreTerms = "", string moreQuery = "") =>
        ListRunsAsync(client, $"filter={Filter($"idempotencyKey:contains:{marker}{moreTerms}")}{moreQuery}");

    public static async Task<JobRunHistoryResponse> GetRunAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(RunPath(id), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = await response.Content.ReadFromJsonAsync<JobRunHistoryResponse>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(history);

        return history;
    }

    public static Task<HttpResponseMessage> PostAsync(HttpClient client, Guid id, string action) =>
        client.PostAsync(RunPath(id, action), content: null, TestContext.Current.CancellationToken);

    public static async Task<JobRunResponse> ReadRunAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var run = await response.Content.ReadFromJsonAsync<JobRunResponse>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(run);

        return run;
    }

    public static async Task<ProblemDetails> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal((int)status, problem.Status);
        Assert.Equal($"/problems/{code}", problem.Type);
        Assert.Equal(code, problem.Extensions[ResultExtensions.CodeExtension]?.ToString());

        return problem;
    }
}
