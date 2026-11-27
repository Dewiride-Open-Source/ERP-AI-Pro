using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Modules.Platform.Approvals.Kinds.Endpoints.Responses;
using Dewiride.Erp.Modules.Platform.Approvals.Requests.Endpoints.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Modules.Platform.Approvals.IntegrationTests.Shared;

// A list a test reads is filtered by its test's kind, so the requests of the other tests of the process never show in it, and a
// decision or a withdrawal reads the request first, so it is made from the version the caller saw.
internal static class ApprovalsApi
{
    public const string Route = "/api/platform/approvals";

    public const string IdempotencyKeyHeader = "Idempotency-Key";

    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static Uri CountsPath { get; } = new($"{Route}/requests/counts", UriKind.Relative);

    public static Uri KindsPath { get; } = new($"{Route}/kinds", UriKind.Relative);

    public static Uri RequestsPath(string query = "") => new($"{Route}/requests{query}", UriKind.Relative);

    public static Uri RequestPath(Guid id) => new($"{Route}/requests/{id}", UriKind.Relative);

    public static Uri ActionPath(Guid id, string action) => new($"{Route}/requests/{id}/{action}", UriKind.Relative);

    public static Uri KindSettingsPath(Guid? id = null) => new(id is { } setting ? $"{Route}/kind-settings/{setting}" : $"{Route}/kind-settings", UriKind.Relative);

    public static Uri RouteFor(string template) => new(Route + template.Replace("{id}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal), UriKind.Relative);

    public static async Task<PagedResponse<ApprovalRequestSummaryResponse>> ListAsync(HttpClient client, ApprovalKind kind, string? box = null, string? filter = null)
    {
        ArgumentNullException.ThrowIfNull(kind);

        var terms = Uri.EscapeDataString(filter is null ? $"kind:eq:{kind.Key}" : $"kind:eq:{kind.Key};{filter}");
        using var response = await client.GetAsync(RequestsPath(box is null ? $"?filter={terms}" : $"?box={box}&filter={terms}"), TestContext.Current.CancellationToken);

        return await ReadAsync<PagedResponse<ApprovalRequestSummaryResponse>>(response, HttpStatusCode.OK);
    }

    public static async Task<ApprovalCountsResponse> CountAsync(HttpClient client)
    {
        using var response = await client.GetAsync(CountsPath, TestContext.Current.CancellationToken);

        return await ReadAsync<ApprovalCountsResponse>(response, HttpStatusCode.OK);
    }

    public static async Task<ApprovalRequestResponse> GetAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(RequestPath(id), TestContext.Current.CancellationToken);

        return await ReadAsync<ApprovalRequestResponse>(response, HttpStatusCode.OK);
    }

    public static Task<HttpResponseMessage> PostAsync(HttpClient client, Guid id, string action, string version, string? comment = null) =>
        client.PostAsJsonAsync(ActionPath(id, action), new { version, comment }, TestContext.Current.CancellationToken);

    public static async Task<ApprovalRequestResponse> ActAsync(HttpClient client, Guid id, string action, string? comment = null)
    {
        var request = await GetAsync(client, id);
        using var response = await PostAsync(client, id, action, request.Version, comment);

        return await ReadAsync<ApprovalRequestResponse>(response, HttpStatusCode.OK);
    }

    public static async Task<IReadOnlyList<ApprovalKindResponse>> KindsAsync(HttpClient client)
    {
        using var response = await client.GetAsync(KindsPath, TestContext.Current.CancellationToken);

        return await ReadAsync<List<ApprovalKindResponse>>(response, HttpStatusCode.OK);
    }

    public static async Task<HttpResponseMessage> SetUpAsync(HttpClient client, ApprovalKind kind, bool required, int workingDaysToDecide, string? idempotencyKey = null)
    {
        ArgumentNullException.ThrowIfNull(kind);

        using var request = new HttpRequestMessage(HttpMethod.Post, KindSettingsPath())
        {
            Content = JsonContent.Create(new { kind = kind.Key, required, workingDaysToDecide }),
        };
        request.Headers.Add(IdempotencyKeyHeader, idempotencyKey ?? Guid.CreateVersion7().ToString());

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static Task<HttpResponseMessage> ChangeAsync(HttpClient client, Guid id, bool required, int workingDaysToDecide, string version) =>
        client.PutAsJsonAsync(KindSettingsPath(id), new { required, workingDaysToDecide, version }, TestContext.Current.CancellationToken);

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<T>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(body);

        return body;
    }

    public static async Task<ValidationProblemDetails> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal((int)status, problem.Status);
        Assert.Equal($"/problems/{code}", problem.Type);
        Assert.Equal(code, problem.Extensions[ResultExtensions.CodeExtension]?.ToString());

        return problem;
    }
}
