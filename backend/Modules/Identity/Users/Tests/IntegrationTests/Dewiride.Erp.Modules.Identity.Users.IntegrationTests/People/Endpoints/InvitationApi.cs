using System.Net;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Graph;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;

// The directory is read with the administrator's own token, so every client signs an administrator in through the real
// session cookie, which puts their account in the token cache; a person signed in by the test header has none. Each test
// signs in an administrator of its own, so a refusal one test makes the token endpoint answer for its administrator reaches
// no other test.
internal static class InvitationApi
{
    public static Uri SearchPath(string text) => PeopleApi.Path($"/directory?search={Uri.EscapeDataString(text)}");

    public static Uri InvitationsPath { get; } = PeopleApi.Path("/invitations");

    public static string UniqueWord() => $"w{Guid.CreateVersion7().ToString("N")[^10..]}";

    public static TestDirectoryPerson DirectoryPerson(string displayName) =>
        new(Guid.CreateVersion7(), displayName, PeopleApi.UniqueWorkEmail(), null);

    public static TestUser NewAdministrator(ErpApiFactory factory)
    {
        var objectId = Guid.CreateVersion7();
        var administrator = new TestUser(objectId, "Vikram Iyer", $"vikram.iyer.{objectId:N}@dewiride.test", [AppRoles.User, AppRoles.Administrator]);
        factory.Services.GetRequiredService<TestTokenEndpoint>().Admit(administrator);

        return administrator;
    }

    public static Task<HttpClient> SignedInAdministratorAsync(ErpApiFactory factory) => SignInAsync(factory, NewAdministrator(factory));

    public static async Task<HttpClient> SignInAsync(ErpApiFactory factory, TestUser person)
    {
        var client = TestSignIn.CreateClient(factory);
        using var signIn = await TestSignIn.SignInAsync(client, person);

        return client;
    }

    public static TestDirectory DirectoryOf(ErpApiFactory factory) => factory.Services.GetRequiredService<TestDirectory>();

    public static async Task<DirectorySearchResponse> SearchAsync(HttpClient client, string text)
    {
        using var response = await client.GetAsync(SearchPath(text), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var search = await response.Content.ReadFromJsonAsync<DirectorySearchResponse>(PeopleApi.Json, TestContext.Current.CancellationToken);
        Assert.NotNull(search);

        return search;
    }

    public static object Invitation(Guid? entraObjectId, string? employeeCode = null) =>
        new { entraObjectId, employeeCode, phoneNumber = "98765 43210", designation = "Accountant", dateOfJoining = "2026-04-01" };

    public static async Task<HttpResponseMessage> PostAsync(HttpClient client, object body, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, InvitationsPath) { Content = JsonContent.Create(body, options: PeopleApi.Json) };
        request.Headers.Add(PeopleApi.IdempotencyKeyHeader, idempotencyKey ?? Guid.CreateVersion7().ToString());

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static async Task<PersonResponse> InviteAsync(HttpClient client, Guid entraObjectId)
    {
        using var response = await PostAsync(client, Invitation(entraObjectId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await PeopleApi.ReadPersonAsync(response);
    }
}
