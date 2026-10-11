using System.Net;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Identity.Users.Departments.Endpoints.Responses;
using Dewiride.Erp.Modules.Identity.Users.Designations.Endpoints.Responses;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.Positions.Endpoints.Responses;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;

internal static class OrganisationApi
{
    public static DateOnly Today => IndianStandardTime.Today(TimeProvider.System);

    public static Uri Departments(string suffix = "") => PeopleApi.Path($"/departments{suffix}");

    public static Uri Designations(string suffix = "") => PeopleApi.Path($"/designations{suffix}");

    public static Uri Positions(Guid person, string suffix = "") => PeopleApi.Path($"/{person}/positions{suffix}");

    public static Uri OrgChart(string query = "") => PeopleApi.Path($"/org-chart{query}");

    public static string UniqueName(string prefix) => $"{prefix} {Guid.CreateVersion7():N}";

    public static Task<HttpResponseMessage> PostAsync(HttpClient client, Uri path, object body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, options: PeopleApi.Json) };
        request.Headers.Add(PeopleApi.IdempotencyKeyHeader, idempotencyKey ?? Guid.CreateVersion7().ToString());

        return SendAsync(client, request);
    }

    public static async Task<DepartmentResponse> CreateDepartmentAsync(HttpClient client, string name, Guid? parentId = null)
    {
        using var response = await PostAsync(client, Departments(), new { name, parentId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadAsync<DepartmentResponse>(response);
    }

    public static async Task<DesignationResponse> CreateDesignationAsync(HttpClient client, string name)
    {
        using var response = await PostAsync(client, Designations(), new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadAsync<DesignationResponse>(response);
    }

    public static Task<HttpResponseMessage> RecordAsync(HttpClient client, Guid person, DateOnly effectiveFrom, Guid? departmentId = null, Guid? designationId = null, Guid? managerId = null) =>
        PostAsync(client, Positions(person), new { effectiveFrom, departmentId, designationId, managerId });

    public static async Task<PositionResponse> RecordPositionAsync(HttpClient client, Guid person, DateOnly effectiveFrom, Guid? departmentId = null, Guid? designationId = null, Guid? managerId = null)
    {
        using var response = await RecordAsync(client, person, effectiveFrom, departmentId, designationId, managerId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadAsync<PositionResponse>(response);
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(PeopleApi.Json, TestContext.Current.CancellationToken);
        Assert.NotNull(value);

        return value;
    }

    public static async Task<PagedResponse<T>> ReadPageAsync<T>(HttpClient client, Uri path)
    {
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadAsync<PagedResponse<T>>(response);
    }

    public static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response));

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage request)
    {
        using (request)
        {
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }
    }
}
