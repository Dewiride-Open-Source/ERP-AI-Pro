using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Authentication.Antiforgery;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Deployment;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Antiforgery;

public sealed class AntiforgeryCookieTests(AntiforgeryCookieTests.Fixture fixture) : IClassFixture<AntiforgeryCookieTests.Fixture>
{
    [Fact]
    public async Task Post_SignIn_IssuesAnHttpOnlyStrictCookieTokenAndAReadableLaxRequestToken()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);

        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        Assert.Equal(["httponly", "path=/", "samesite=strict", "secure"], AttributesOf(signIn, "__Host-erp-antiforgery"));
        Assert.Equal(["path=/", "samesite=lax", "secure"], AttributesOf(signIn, "__Host-erp-xsrf"));
    }

    [Fact]
    public async Task Post_SignInAgainOverASessionTheCheckRefuses_KeepsACookieTokenThatCarriesChanges()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        using var firstSignIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        await TestSignIn.ForgetAccountAsync(fixture.Factory.Services, TestUsers.Accountant);

        using var secondSignIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        using var change = await client.PostAsync(new Uri(TokenCookies.ProtectedChangesPath, UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
    }

    [Fact]
    public async Task Get_AntiforgerySignedIn_IssuesANewRequestTokenForThePersonAndKeepsTheCookieToken()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        using var response = await client.GetAsync(new Uri(AuthPaths.Antiforgery, UriKind.Relative), TestContext.Current.CancellationToken);
        using var change = await PostChangeAsync(client, TokenCookies.RequestTokenOf(response));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(TokenCookies.SetCookieOf(response, AntiforgeryTokens.CookieName));
        Assert.Equal(["path=/", "samesite=lax", "secure"], AttributesOf(response, AntiforgeryTokens.RequestTokenCookieName));
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
    }

    [Fact]
    public async Task Get_AntiforgerySignedInWithoutTheCookieToken_IssuesBothTokens()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        var session = (TokenCookies.SessionCookieName, TokenCookies.ValueOf(signIn, TokenCookies.SessionCookieName));

        using var response = await TokenCookies.SendWithAsync(fixture.Factory, HttpMethod.Get, AuthPaths.Antiforgery, TokenCookies.CookieHeader(session), requestToken: null);
        var cookies = TokenCookies.CookieHeader(session, (AntiforgeryTokens.CookieName, TokenCookies.ValueOf(response, AntiforgeryTokens.CookieName)));
        using var change = await TokenCookies.SendWithAsync(fixture.Factory, HttpMethod.Post, TokenCookies.ChangesPath, cookies, TokenCookies.RequestTokenOf(response));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
    }

    [Fact]
    public async Task Get_AntiforgerySignedInWithACookieTokenThatDoesNotDeserialize_IssuesANewPairThatCarriesChanges()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        var session = (TokenCookies.SessionCookieName, TokenCookies.ValueOf(signIn, TokenCookies.SessionCookieName));
        var damaged = TokenCookies.CookieHeader(session, (AntiforgeryTokens.CookieName, "not-a-cookie-token-the-api-issued"));

        using var refused = await TokenCookies.SendWithAsync(fixture.Factory, HttpMethod.Post, TokenCookies.ProtectedChangesPath, damaged, TokenCookies.RequestTokenOf(signIn));
        using var response = await TokenCookies.SendWithAsync(fixture.Factory, HttpMethod.Get, AuthPaths.Antiforgery, damaged, requestToken: null);
        var renewed = TokenCookies.CookieHeader(session, (AntiforgeryTokens.CookieName, TokenCookies.ValueOf(response, AntiforgeryTokens.CookieName)));
        using var change = await TokenCookies.SendWithAsync(fixture.Factory, HttpMethod.Post, TokenCookies.ProtectedChangesPath, renewed, TokenCookies.RequestTokenOf(response));

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("antiforgery.token-invalid", await CodeOfAsync(refused));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
    }

    [Theory]
    [InlineData("cross-site")]
    [InlineData("same-site")]
    [InlineData("none")]
    public async Task Get_AntiforgerySentByTheBrowserForAnotherSiteOrTheAddressBar_AnswersAProblemAndSetsNoCookie(string fetchSite)
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        using var request = new HttpRequestMessage(HttpMethod.Get, AuthPaths.Antiforgery);
        request.Headers.Add("Sec-Fetch-Site", fetchSite);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request.invalid", await CodeOfAsync(response));
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Get_AntiforgerySentByAScriptOfThisSite_IssuesANewRequestToken()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        using var request = new HttpRequestMessage(HttpMethod.Get, AuthPaths.Antiforgery);
        request.Headers.Add("Sec-Fetch-Site", "same-origin");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var change = await PostChangeAsync(client, TokenCookies.RequestTokenOf(response));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
    }

    [Fact]
    public async Task Get_AntiforgeryAnonymously_AnswersUnauthenticatedAndSetsNoCookie()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);

        using var response = await client.GetAsync(new Uri(AuthPaths.Antiforgery, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Post_Logout_ClearsBothAntiforgeryCookies()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        using var response = await client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        AssertCleared(response, AntiforgeryTokens.CookieName);
        AssertCleared(response, AntiforgeryTokens.RequestTokenCookieName);
    }

    [Fact]
    public async Task Get_WithASessionTheCheckRefuses_ClearsTheSessionAndBothAntiforgeryCookies()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        await TestSignIn.ForgetAccountAsync(fixture.Factory.Services, TestUsers.Accountant);

        using var response = await client.GetAsync(new Uri(TokenCookies.ChangesPath, UriKind.Relative), TestContext.Current.CancellationToken);

        AssertCleared(response, TokenCookies.SessionCookieName);
        AssertCleared(response, AntiforgeryTokens.CookieName);
        AssertCleared(response, AntiforgeryTokens.RequestTokenCookieName);
    }

    [Fact]
    public async Task Post_WithTokensIssuedBeforeTheApiRestarted_ReachesTheEndpoint()
    {
        using var deployment = TestDeployment.WithPersistedKeyRing();
        string cookies;
        string requestToken;
        await using (var beforeRestart = Host(deployment))
        {
            using var client = TestSignIn.CreateClient(beforeRestart);
            using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
            cookies = TokenCookies.CookieHeader(
                (TokenCookies.SessionCookieName, TokenCookies.ValueOf(signIn, TokenCookies.SessionCookieName)),
                (AntiforgeryTokens.CookieName, TokenCookies.ValueOf(signIn, AntiforgeryTokens.CookieName)));
            requestToken = TokenCookies.RequestTokenOf(signIn);
        }

        await using var afterRestart = Host(deployment);
        using var response = await TokenCookies.SendWithAsync(afterRestart, HttpMethod.Post, TokenCookies.ProtectedChangesPath, cookies, requestToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Post_LogoutWithTheTokenInTheFormField_SignsThePersonOut()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Administrator);
        using var form = new FormUrlEncodedContent([KeyValuePair.Create("__RequestVerificationToken", TokenCookies.RequestTokenOf(signIn))]);

        using var response = await client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(TestIdentityProvider.EndSessionEndpoint.AbsoluteUri, response.Headers.Location?.GetLeftPart(UriPartial.Path));
        Assert.False(await TestSignIn.IsAccountCachedAsync(fixture.Factory.Services, TestUsers.Administrator));
    }

    [Fact]
    public async Task Post_LogoutWithoutAToken_AnswersTokenMissingAndKeepsTheSession()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Administrator);

        using var response = await client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        using var change = await PostChangeAsync(client, TokenCookies.RequestTokenOf(signIn));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AntiforgeryProblems.TokenMissing, await CodeOfAsync(response));
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.True(await TestSignIn.IsAccountCachedAsync(fixture.Factory.Services, TestUsers.Administrator));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("returnUrl")]
    public async Task Post_LogoutWithAFormWithoutTheTokenField_AnswersTokenMissingAndKeepsTheSession(string? field)
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Administrator);
        using var form = new FormUrlEncodedContent(field is null ? [] : [KeyValuePair.Create(field, "/")]);

        using var response = await client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), form, TestContext.Current.CancellationToken);
        using var change = await PostChangeAsync(client, TokenCookies.RequestTokenOf(signIn));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AntiforgeryProblems.TokenMissing, await CodeOfAsync(response));
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.True(await TestSignIn.IsAccountCachedAsync(fixture.Factory.Services, TestUsers.Administrator));
    }

    [Fact]
    public async Task Post_LogoutWithAFormTokenTheApiNeverIssued_AnswersTokenInvalidAndKeepsTheSession()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Administrator);
        using var form = new FormUrlEncodedContent([KeyValuePair.Create(AntiforgeryTokens.FormFieldName, "not-a-token-the-api-issued")]);

        using var response = await client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), form, TestContext.Current.CancellationToken);
        using var change = await PostChangeAsync(client, TokenCookies.RequestTokenOf(signIn));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AntiforgeryProblems.TokenInvalid, await CodeOfAsync(response));
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
    }

    private static ErpApiFactory Host(TestDeployment deployment) =>
        new ErpApiFactory().WithDeployment(deployment).WithTestEndpoints(TokenCookies.MapSignInAndChanges);

    private static async Task<HttpResponseMessage> PostChangeAsync(HttpClient client, string requestToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenCookies.ProtectedChangesPath);
        request.Headers.Add("X-XSRF-TOKEN", requestToken);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string[] AttributesOf(HttpResponseMessage response, string name) =>
        [.. (TokenCookies.SetCookieOf(response, name) ?? throw new InvalidOperationException($"The response sets no {name} cookie."))
            .Split(';').Skip(1).Select(attribute => attribute.Trim().ToLowerInvariant()).Order(StringComparer.Ordinal)];

    private static void AssertCleared(HttpResponseMessage response, string name)
    {
        var cleared = TokenCookies.SetCookieOf(response, name);
        Assert.NotNull(cleared);
        Assert.Contains(TokenCookies.Cleared, cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cleared, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("code").GetString();
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithTestEndpoints(TokenCookies.MapSignInAndChanges);

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
