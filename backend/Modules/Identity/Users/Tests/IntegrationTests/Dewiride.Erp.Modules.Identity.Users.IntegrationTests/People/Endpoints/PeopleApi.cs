using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;

internal static class PeopleApi
{
    public const string Route = "/api/identity/users";

    public const string IdempotencyKeyHeader = "Idempotency-Key";

    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static Uri Path(string suffix = "") => new(Route + suffix, UriKind.Relative);

    public static string UniqueWorkEmail() => $"person-{Guid.CreateVersion7():N}@dewiride.test";

    public static string UniqueEmployeeCode() => $"T-{Guid.CreateVersion7().ToString("N")[^12..]}";

    public static object Person(string workEmail, string? employeeCode = null, Guid? entraObjectId = null) =>
        new { entraObjectId, displayName = "Meera Nair", workEmail, employeeCode, phoneNumber = "98765 43210", designation = "Accountant", dateOfJoining = "2026-04-01" };

    public static Task<HttpResponseMessage> PostAsync(HttpClient client, object body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Path()) { Content = JsonContent.Create(body, options: Json) };
        request.Headers.Add(IdempotencyKeyHeader, idempotencyKey ?? Guid.CreateVersion7().ToString());

        return SendAsync(client, request);
    }

    public static async Task<PersonResponse> RegisterAsync(HttpClient client, object body)
    {
        using var response = await PostAsync(client, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadPersonAsync(response);
    }

    public static async Task<PersonResponse> ReadPersonAsync(HttpResponseMessage response)
    {
        var person = await response.Content.ReadFromJsonAsync<PersonResponse>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(person);

        return person;
    }

    public static async Task<PagedResponse<PersonResponse>> ReadPageAsync(HttpResponseMessage response)
    {
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<PersonResponse>>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(page);

        return page;
    }

    public static async Task<ProblemDetails> ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(problem);

        return problem;
    }

    public static async Task<HttpValidationProblemDetails> ReadValidationProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(problem);

        return problem;
    }

    public static string? CodeOf(ProblemDetails problem) =>
        problem.Extensions.TryGetValue("code", out var code) ? code?.ToString() : null;

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage request)
    {
        using (request)
        {
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }
    }
}
