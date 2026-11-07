using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.ParameterSets.Endpoints;

// Every host of the test process keeps its change records in the one test database, so a test reads only the change records
// filed under the sets it changed, by their ids.
internal static class ParameterSetsApi
{
    public const string Route = "/api/compliance/statutory-parameters/parameter-sets";

    public const string SetChangeKey = "compliance.statutory-parameters.parameter-sets";

    public const string VersionChangeKey = "compliance.statutory-parameters.parameter-versions";

    public const string RowChangeKey = "compliance.statutory-parameters.parameter-rows";

    private const string ChangesRoute = "/api/platform/audit/changes";

    private static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static Uri Path(string suffix = "") => new(Route + suffix, UriKind.Relative);

    public static async Task<ParameterSetsResponse> ListAsync(HttpClient client)
    {
        using var response = await client.GetAsync(Path(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadAsync<ParameterSetsResponse>(response);
    }

    public static async Task<ParameterSetResponse> GetAsync(HttpClient client, string key, string? on = null)
    {
        using var response = await client.GetAsync(Path($"/{key}{(on is null ? string.Empty : $"?on={on}")}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadAsync<ParameterSetResponse>(response);
    }

    public static async Task<IReadOnlyList<RecordedChange>> ChangesOfAsync(HttpClient administrator, Guid subjectId)
    {
        var filter = Uri.EscapeDataString($"subjectId:eq:{subjectId}");
        using var response = await administrator.GetAsync(new Uri($"{ChangesRoute}?sort=occurredAt:asc&pageSize=200&filter={filter}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await ReadAsync<PagedResponse<RecordedChange>>(response)).Items;
    }

    public static async Task<long> ChangeCountAsync(HttpClient administrator, string changeKey)
    {
        var filter = Uri.EscapeDataString($"entityType:eq:{changeKey}");
        using var response = await administrator.GetAsync(new Uri($"{ChangesRoute}?pageSize=1&filter={filter}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await ReadAsync<PagedResponse<RecordedChange>>(response)).TotalCount;
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(value);

        return value;
    }

    public static Task<ProblemDetails> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code) =>
        ReadProblemAsync<ProblemDetails>(response, status, code);

    public static Task<HttpValidationProblemDetails> AssertInvalidRequestAsync(HttpResponseMessage response) =>
        ReadProblemAsync<HttpValidationProblemDetails>(response, HttpStatusCode.BadRequest, "request.invalid");

    private static async Task<TProblem> ReadProblemAsync<TProblem>(HttpResponseMessage response, HttpStatusCode status, string code)
        where TProblem : ProblemDetails
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<TProblem>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal((int)status, problem.Status);
        Assert.Equal($"/problems/{code}", problem.Type);
        Assert.Equal(code, problem.Extensions[ResultExtensions.CodeExtension]?.ToString());

        return problem;
    }
}
