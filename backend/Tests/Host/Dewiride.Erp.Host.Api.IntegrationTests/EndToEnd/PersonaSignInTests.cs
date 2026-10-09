using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Authentication.Antiforgery;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.EndToEnd;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Host.Api.IntegrationTests.EndToEnd;

public sealed class PersonaSignInTests(PersonaSignInTests.Fixture fixture) : IClassFixture<PersonaSignInTests.Fixture>
{
    private const string SessionCookie = "__Host-erp-session";

    [Fact]
    public async Task Post_KnownPersona_SignsInANewPersonOfThePersonaWhoseSessionTheApiAccepts()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);

        using var response = await SignInAsync(client, "administrator");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var person = await response.Content.ReadFromJsonAsync<PersonaSignInResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(person);
        Assert.NotEqual(TestUsers.Administrator.ObjectId, person.Id);
        Assert.Equal(TestUsers.Administrator.Name, person.Name);
        Assert.Equal(TestUsers.Administrator.UserName, person.UserName);
        Assert.Equal(TestUsers.Administrator.Roles, person.Roles);
        Assert.Equal(BearerTokenOptionsSetup.IssuerOf(fixture.Factory.Services.GetRequiredService<IOptions<EntraSignInOptions>>().Value), person.Issuer);
        Assert.True(Guid.TryParse(person.EntraSessionId, out var entraSessionId));
        Assert.NotEqual(person.Id, entraSessionId);
        Assert.NotEqual(TestUsers.Administrator.EntraSessionId, person.EntraSessionId);
        using var me = JsonDocument.Parse(await client.GetStringAsync(new Uri(AuthPaths.Me, UriKind.Relative), TestContext.Current.CancellationToken));
        Assert.Equal(person.Id, me.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(person.Name, me.RootElement.GetProperty("name").GetString());
        Assert.Equal(person.UserName, me.RootElement.GetProperty("userName").GetString());
        Assert.Equal(person.Roles, me.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
    }

    [Fact]
    public async Task Post_KnownPersona_AnswersThePersonTheirEntraSessionAndItsIssuerInThisShape()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);

        using var response = await SignInAsync(client, "accountant");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["id", "name", "userName", "roles", "entraSessionId", "issuer"], body.RootElement.EnumerateObject().Select(member => member.Name));
    }

    [Fact]
    public async Task Get_FrontChannelSignOutOfTheEntraSessionAPersonaSignInNames_EndsThatSessionOnly()
    {
        using var leaving = TestSignIn.CreateClient(fixture.Factory);
        using var staying = TestSignIn.CreateClient(fixture.Factory);
        using var leavingSignIn = await SignInAsync(leaving, "accountant");
        using var stayingSignIn = await SignInAsync(staying, "accountant");
        var person = await leavingSignIn.Content.ReadFromJsonAsync<PersonaSignInResponse>(TestContext.Current.CancellationToken);
        using var entra = fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        using var signOut = await entra.GetAsync(
            new Uri($"{AuthPaths.FrontChannelSignOut}?iss={Uri.EscapeDataString(person!.Issuer)}&sid={Uri.EscapeDataString(person.EntraSessionId)}", UriKind.Relative),
            TestContext.Current.CancellationToken);
        using var left = await leaving.GetAsync(new Uri(AuthPaths.Me, UriKind.Relative), TestContext.Current.CancellationToken);
        using var stayed = await staying.GetAsync(new Uri(AuthPaths.Me, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, left.StatusCode);
        Assert.Equal(HttpStatusCode.OK, stayed.StatusCode);
    }

    [Fact]
    public async Task Post_SamePersonaTwice_SignsInTwoDifferentPeople()
    {
        using var first = TestSignIn.CreateClient(fixture.Factory);
        using var second = TestSignIn.CreateClient(fixture.Factory);

        using var firstSignIn = await SignInAsync(first, "accountant");
        using var secondSignIn = await SignInAsync(second, "accountant");

        var firstPerson = await firstSignIn.Content.ReadFromJsonAsync<PersonaSignInResponse>(TestContext.Current.CancellationToken);
        var secondPerson = await secondSignIn.Content.ReadFromJsonAsync<PersonaSignInResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(firstPerson);
        Assert.NotNull(secondPerson);
        Assert.NotEqual(firstPerson.Id, secondPerson.Id);
        Assert.NotEqual(firstPerson.EntraSessionId, secondPerson.EntraSessionId);
    }

    [Fact]
    public async Task Post_KnownPersona_IssuesTheAntiforgeryPairThatCarriesThePersonsChanges()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        using var withoutToken = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);

        using var response = await SignInAsync(client, "accountant");
        using var renewal = await client.PostAsync(new Uri(AuthPaths.Session, UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        using var refused = await PostWithCookiesOfAsync(withoutToken, response, AuthPaths.Session);

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(cookies, cookie => cookie.StartsWith($"{SessionCookie}=", StringComparison.Ordinal));
        Assert.Contains(cookies, cookie => cookie.StartsWith($"{AntiforgeryTokens.CookieName}=", StringComparison.Ordinal));
        Assert.Contains(cookies, cookie => cookie.StartsWith($"{AntiforgeryTokens.RequestTokenCookieName}=", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, renewal.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var problem = await refused.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(AntiforgeryProblems.TokenMissing, problem!.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Post_LogoutOfOnePersonOfAPersona_LeavesTheOtherPeopleOfThePersonaSignedIn()
    {
        using var leaving = TestSignIn.CreateClient(fixture.Factory);
        using var staying = TestSignIn.CreateClient(fixture.Factory);
        using var leavingSignIn = await SignInAsync(leaving, "accountant");
        using var stayingSignIn = await SignInAsync(staying, "accountant");

        using var logout = await leaving.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        using var left = await leaving.GetAsync(new Uri(AuthPaths.Me, UriKind.Relative), TestContext.Current.CancellationToken);
        using var stayed = await staying.GetAsync(new Uri(AuthPaths.Me, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, left.StatusCode);
        Assert.Equal(HttpStatusCode.OK, stayed.StatusCode);
    }

    [Fact]
    public async Task Post_UnknownPersona_AnswersNotFoundAndSignsNobodyIn()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);

        using var response = await SignInAsync(client, "auditor");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Map_InTheDevelopmentEnvironment_StopsTheHostFromStarting()
    {
        await using var factory = new ErpApiFactory().WithTestEndpoints(PersonaSignIn.Map);

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains(EndToEndHost.EnvironmentName, exception.Message, StringComparison.Ordinal);
    }

    private static Task<HttpResponseMessage> SignInAsync(HttpClient client, string persona) =>
        client.PostAsync(new Uri($"{PersonaSignIn.PathPrefix}/{persona}", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

    private static async Task<HttpResponseMessage> PostWithCookiesOfAsync(HttpClient client, HttpResponseMessage signIn, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Cookie", string.Join("; ", signIn.Headers.GetValues("Set-Cookie").Select(cookie => cookie.Split(';')[0])));

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = ErpApiFactory.ForEnvironment(EndToEndHost.EnvironmentName).WithTestEndpoints(PersonaSignIn.Map);

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
