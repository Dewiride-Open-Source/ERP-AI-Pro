using System.Net;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Authentication.OpenIdConnect;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Authentication;

public sealed class LogoutEndpointTests(LogoutEndpointTests.Fixture fixture) : IClassFixture<LogoutEndpointTests.Fixture>
{
    private const string SessionCookie = "__Host-erp-session";

    private const string SignedInPath = "/__test/signed-in";

    [Fact]
    public async Task Post_LogoutWithASession_ClearsTheCookieAndRedirectsToTheEndSessionEndpoint()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        await AssertStatusAsync(client, HttpStatusCode.OK);
        Assert.True(await TestSignIn.IsAccountCachedAsync(fixture.Factory.Services, TestUsers.Accountant));

        using var response = await client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.False(await TestSignIn.IsAccountCachedAsync(fixture.Factory.Services, TestUsers.Accountant));
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal(TestIdentityProvider.EndSessionEndpoint.AbsoluteUri, location.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal($"{TestIdentityProvider.WebOrigin}{AuthPaths.SignedOutCallback}", Assert.Single(query["post_logout_redirect_uri"]));
        Assert.Equal(AuthPaths.SignedOutPage, StateOf(Assert.Single(query["state"])!)?.RedirectUri);
        var cleared = Assert.Single(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith($"{SessionCookie}=", StringComparison.Ordinal));
        Assert.Contains("expires=Thu, 01 Jan 1970", cleared, StringComparison.OrdinalIgnoreCase);
        await AssertStatusAsync(client, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_LogoutWithASession_SendsTheLoginHintOfThePersonAsTheLogoutHint()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Administrator);

        using var response = await client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.Equal(TestUsers.Administrator.LoginHint, Assert.Single(query[SignInEvents.LogoutHintParameter]));
        Assert.DoesNotContain(Uri.EscapeDataString(TestUsers.Administrator.UserName), response.Headers.Location.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_LogoutAnonymously_AnswersUnauthenticatedWithoutALocation()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);

        using var response = await client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(ProblemTypes.RequestUnauthenticated, problem!.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Get_SignedOutCallback_RedirectsToTheSignedOutNoticeOfTheSignInPage()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        using var logout = await client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        var state = Assert.Single(QueryHelpers.ParseQuery(logout.Headers.Location!.Query)["state"]);

        using var response = await client.GetAsync(new Uri($"{AuthPaths.SignedOutCallback}?state={Uri.EscapeDataString(state!)}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(AuthPaths.SignedOutPage, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Get_FrontChannelSignOutWithOnlyTheSessionCookie_AnswersAValidationProblemAndLeavesThePersonSignedIn()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Administrator);

        using var response = await client.GetAsync(new Uri(AuthPaths.FrontChannelSignOut, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(ProblemTypes.RequestInvalid, problem!.Extensions["code"]?.ToString());
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
        Assert.DoesNotContain(cookies, cookie => cookie.StartsWith($"{SessionCookie}=", StringComparison.Ordinal));
        await AssertStatusAsync(client, HttpStatusCode.OK);
        Assert.True(await TestSignIn.IsAccountCachedAsync(fixture.Factory.Services, TestUsers.Administrator));
    }

    private AuthenticationProperties? StateOf(string state) =>
        fixture.Factory.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme)
            .StateDataFormat.Unprotect(state);

    private static async Task AssertStatusAsync(HttpClient client, HttpStatusCode status)
    {
        using var response = await client.GetAsync(new Uri(SignedInPath, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(status, response.StatusCode);
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithTestEndpoints(routes =>
        {
            TestSignIn.Map(routes);
            routes.MapGet(SignedInPath, () => Results.Ok());
        });

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
