using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Authentication.Antiforgery;
using Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;
using Dewiride.Erp.Host.Api.IntegrationTests.Antiforgery;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authentication.BearerTokens;

namespace Dewiride.Erp.Host.Api.IntegrationTests.BearerTokens;

public sealed class BearerTokenRouteTests(BearerTokenRouteTests.Fixture fixture) : IClassFixture<BearerTokenRouteTests.Fixture>
{
    [Fact]
    public async Task Get_BearerRouteWithOnlyTheSessionCookie_AnswersUnauthenticatedNamingTheBearerScheme()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, BearerTokenRoutes.Path, token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(["Bearer"], response.Headers.WwwAuthenticate.Select(value => value.ToString()));
    }

    [Fact]
    public async Task Post_SessionRouteWithOnlyABearerToken_AnswersUnauthenticatedWithoutAChallenge()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Post, BearerTokenRoutes.SessionPath, BearerTokenRoutes.PersonToken(BearerTokenRoutes.ReadScope));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(response.Headers.WwwAuthenticate);
    }

    [Fact]
    public async Task Post_BearerRouteWithTheTokenAndTheSessionCookieWithoutTheRequestToken_ReachesTheRouteAsTheTokenHolder()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Administrator);

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Post, BearerTokenRoutes.Path, BearerTokenRoutes.ApplicationToken(BearerTokenRoutes.IntegrationRole));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new BearerTokenRoutes.ObservedActor(TestApplications.Integration.ObjectId, true), await response.Content.ReadFromJsonAsync<BearerTokenRoutes.ObservedActor>(TestContext.Current.CancellationToken));
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Post_SessionRouteWithTheCookieAndABearerTokenWithoutTheRequestToken_AnswersTokenMissing()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Post, BearerTokenRoutes.SessionPath, BearerTokenRoutes.ApplicationToken(BearerTokenRoutes.IntegrationRole));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(AntiforgeryProblems.TokenMissing, body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Get_BearerRouteWithACookieOfAPersonWhoSignedOut_LeavesTheCookieAloneWhileTheSessionRouteClearsIt()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        var cookie = TokenCookies.CookieHeader((TokenCookies.SessionCookieName, TokenCookies.ValueOf(signIn, TokenCookies.SessionCookieName)));
        using var signOut = await client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        using var other = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);

        using var bearerRoute = await SendWithCookieAsync(other, BearerTokenRoutes.Path, cookie, BearerTokenRoutes.ApplicationToken(BearerTokenRoutes.IntegrationRole));
        using var sessionRoute = await SendWithCookieAsync(other, BearerTokenRoutes.SessionPath, cookie, token: null);

        Assert.Equal(HttpStatusCode.Found, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.OK, bearerRoute.StatusCode);
        Assert.False(bearerRoute.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.Unauthorized, sessionRoute.StatusCode);
        Assert.Contains(TokenCookies.Cleared, TokenCookies.SetCookieOf(sessionRoute, TokenCookies.SessionCookieName), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_TokensOfAnApplicationAndAPersonFromOneAddress_EachHaveTheirOwnAllowance()
    {
        await using var factory = BearerTokenRoutes.Factory()
            .WithConfiguration($"{RateLimitingOptions.SectionName}:ActorPermitLimit", "2")
            .WithConfiguration($"{RateLimitingOptions.SectionName}:AnonymousPermitLimit", "2");
        using var client = factory.CreateClient();
        var application = BearerTokenRoutes.ApplicationToken(BearerTokenRoutes.IntegrationRole);
        var person = BearerTokenRoutes.PersonToken(BearerTokenRoutes.ReadScope);

        var applicationStatuses = await StatusesAsync(client, application, times: 3);
        var personStatuses = await StatusesAsync(client, person, times: 2);

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], applicationStatuses);
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK], personStatuses);
    }

    private static async Task<List<HttpStatusCode>> StatusesAsync(HttpClient client, string token, int times)
    {
        List<HttpStatusCode> statuses = [];
        for (var request = 0; request < times; request++)
        {
            using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, BearerTokenRoutes.Path, token);
            statuses.Add(response.StatusCode);
        }

        return statuses;
    }

    private static async Task<HttpResponseMessage> SendWithCookieAsync(HttpClient client, string path, string cookie, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", cookie);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = BearerTokenRoutes.Factory();

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
