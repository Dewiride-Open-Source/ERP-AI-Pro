using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Modules.Platform.Audit.Changes.Endpoints.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Modules.Platform.Audit.IntegrationTests.Changes.Endpoints;

// The changes a test reads are those of departments it made itself, filtered by their ids, because every host of the test
// process records its changes in the one test database.
internal static class ChangesApi
{
    public const string Route = "/api/platform/audit/changes";

    public const string DepartmentKey = "identity.users.departments";

    private const string DepartmentsRoute = "/api/identity/users/departments";

    private const string IdempotencyKeyHeader = "Idempotency-Key";

    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static Uri Path(string query = "") => new(Route + query, UriKind.Relative);

    public static Uri ExportPath(string filter) => new($"{Route}/export?filter={Uri.EscapeDataString(filter)}", UriKind.Relative);

    public static string Filter(string filter) => Uri.EscapeDataString(filter);

    public static string UniqueName(string prefix) => $"{prefix} {Guid.CreateVersion7():N}";

    public static string CorrelationIdOf(HttpResponseMessage response) => Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName));

    public static async Task<PagedResponse<ChangeRecordResponse>> ListAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync(Path($"?{query}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ChangeRecordResponse>>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(page);

        return page;
    }

    public static Task<PagedResponse<ChangeRecordResponse>> ListOfAsync(HttpClient client, Guid subjectId, string moreTerms = "") =>
        ListAsync(client, $"filter={Filter($"subjectId:eq:{subjectId}{moreTerms}")}");

    public static async Task<HttpResponseMessage> PostDepartmentAsync(HttpClient client, string name, Guid? parentId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(DepartmentsRoute, UriKind.Relative))
        {
            Content = JsonContent.Create(new { name, parentId }, options: Json),
        };
        request.Headers.Add(IdempotencyKeyHeader, Guid.CreateVersion7().ToString());

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static async Task<TestDepartment> CreateDepartmentAsync(HttpClient client, string name, Guid? parentId = null)
    {
        using var response = await PostDepartmentAsync(client, name, parentId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadDepartmentAsync(response);
    }

    public static Task<HttpResponseMessage> PutDepartmentAsync(HttpClient client, TestDepartment department, string name, Guid? parentId) =>
        client.PutAsJsonAsync(new Uri($"{DepartmentsRoute}/{department.Id}", UriKind.Relative), new { name, parentId, version = department.Version }, Json, TestContext.Current.CancellationToken);

    public static async Task<TestDepartment> UpdateDepartmentAsync(HttpClient client, TestDepartment department, string name, Guid? parentId)
    {
        using var response = await PutDepartmentAsync(client, department, name, parentId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadDepartmentAsync(response);
    }

    public static async Task DeleteDepartmentAsync(HttpClient client, Guid id)
    {
        using var response = await client.DeleteAsync(new Uri($"{DepartmentsRoute}/{id}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    public static async Task<TestDepartment> ReadDepartmentAsync(HttpResponseMessage response)
    {
        var department = await response.Content.ReadFromJsonAsync<TestDepartment>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(department);

        return department;
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
