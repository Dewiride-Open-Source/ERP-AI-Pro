using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Idempotency.Http;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Admission;

// Every test host of the process writes to the one test database, so a test that deactivates or deletes a record does so
// to a person of its own, never to a persona other tests sign in; an administrator changes the record through the API.
internal static class PersonRecords
{
    private const string PeoplePath = "/api/identity/users";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static TestUser NewPerson(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var objectId = Guid.CreateVersion7();
        var person = new TestUser(objectId, "Ravi Kumar", $"ravi.kumar.{objectId:N}@example.com", [AppRoles.User]);
        factory.Services.GetRequiredService<TestTokenEndpoint>().Admit(person);

        return person;
    }

    public static async Task<User?> OfAsync(WebApplicationFactory<Program> factory, TestUser person)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(person);

        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<UsersDbContext>().Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.EntraObjectId == person.ObjectId, TestContext.Current.CancellationToken);
    }

    public static async Task<Guid> RegisterAsync(WebApplicationFactory<Program> factory, TestUser person)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(person);

        using var administrator = factory.CreateClient().AsUser(TestUsers.Administrator);
        using var request = new HttpRequestMessage(HttpMethod.Post, PeoplePath)
        {
            Content = JsonContent.Create(new { entraObjectId = person.ObjectId, displayName = person.Name, workEmail = person.UserName }, options: Json),
        };
        request.Headers.Add(IdempotencyKeyHeader.Name, Guid.CreateVersion7().ToString());
        using var response = await administrator.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("id").GetGuid();
    }

    public static Task DeactivateAsync(WebApplicationFactory<Program> factory, TestUser person) => ChangeStatusAsync(factory, person, "deactivated");

    public static Task ReactivateAsync(WebApplicationFactory<Program> factory, TestUser person) => ChangeStatusAsync(factory, person, "active");

    public static async Task DeleteAsync(WebApplicationFactory<Program> factory, TestUser person)
    {
        var user = await OfAsync(factory, person);
        Assert.NotNull(user);
        using var administrator = factory.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await administrator.DeleteAsync(new Uri($"{PeoplePath}/{user.Id.Value}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task ChangeStatusAsync(WebApplicationFactory<Program> factory, TestUser person, string status)
    {
        var user = await OfAsync(factory, person);
        Assert.NotNull(user);
        using var administrator = factory.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await administrator.PutAsJsonAsync(
            new Uri($"{PeoplePath}/{user.Id.Value}/status", UriKind.Relative),
            new { status, version = Convert.ToBase64String(user.RowVersion) },
            Json,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
