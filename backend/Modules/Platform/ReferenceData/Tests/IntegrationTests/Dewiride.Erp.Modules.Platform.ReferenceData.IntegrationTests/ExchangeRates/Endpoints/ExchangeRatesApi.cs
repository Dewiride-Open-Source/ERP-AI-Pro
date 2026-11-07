using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Endpoints.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IntegrationTests.ExchangeRates.Endpoints;

// Every host of the test process keeps its records and their change records in the one test database, and a currency has one
// rate a day, so each test records its rates on days no other test of the process uses and reads back only those days.
internal static class ExchangeRatesApi
{
    public const string Route = "/api/platform/reference-data/exchange-rates";

    public const string ChangeKey = "platform.reference-data.exchange-rates";

    public const string IdempotencyReplayedHeader = "Idempotency-Replayed";

    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private const string ChangesRoute = "/api/platform/audit/changes";

    private static readonly DateOnly FirstTestDay = new(2000, 1, 1);

    private static int _daysTaken;

    private static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static Uri Path(string suffix = "") => new(Route + suffix, UriKind.Relative);

    public static DateOnly UniqueDay() => FirstTestDay.AddDays(Interlocked.Increment(ref _daysTaken));

    public static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Filter(string filter) => Uri.EscapeDataString(filter);

    public static object Rate(string currency, DateOnly day, decimal rate, int unit = 1, string? source = null) =>
        new { currency, rateDate = Day(day), rate, unit, source };

    public static async Task<HttpResponseMessage> PostAsync(HttpClient client, object body, string suffix = "", string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Path(suffix)) { Content = JsonContent.Create(body, options: Json) };
        request.Headers.Add(IdempotencyKeyHeader, idempotencyKey ?? Guid.CreateVersion7().ToString());

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static Task<HttpResponseMessage> ImportAsync(HttpClient client, params object[] rates) =>
        PostAsync(client, new { rates }, "/imports");

    public static async Task<ExchangeRateResponse> RecordAsync(HttpClient client, string currency, DateOnly day, decimal rate, int unit = 1, string? source = null)
    {
        using var response = await PostAsync(client, Rate(currency, day, rate, unit, source));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadAsync<ExchangeRateResponse>(response);
    }

    public static async Task<HttpResponseMessage> CorrectAsync(HttpClient client, Guid id, object body) =>
        await client.PutAsJsonAsync(Path($"/{id}"), body, Json, TestContext.Current.CancellationToken);

    public static async Task<PagedResponse<ExchangeRateResponse>> ListAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync(Path($"?{query}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadAsync<PagedResponse<ExchangeRateResponse>>(response);
    }

    public static async Task<IReadOnlyList<RecordedChange>> ChangesOfAsync(HttpClient administrator, Guid subjectId)
    {
        var filter = Filter($"subjectId:eq:{subjectId}");
        using var response = await administrator.GetAsync(new Uri($"{ChangesRoute}?sort=occurredAt:asc&pageSize=200&filter={filter}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await ReadAsync<PagedResponse<RecordedChange>>(response)).Items;
    }

    public static async Task<long> ChangeCountAsync(HttpClient administrator, string changeKey)
    {
        var filter = Filter($"entityType:eq:{changeKey}");
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

    public static Task<HttpValidationProblemDetails> AssertInvalidAsync(HttpResponseMessage response, string code) =>
        ReadProblemAsync<HttpValidationProblemDetails>(response, HttpStatusCode.BadRequest, code);

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
