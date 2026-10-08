using System.Net;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Authentication.OpenIdConnect;
using Dewiride.Erp.BuildingBlocks.Authentication.Sessions;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Deployment;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.TokenCache;

public sealed class TokenCacheFailureTests
{
    private const string ReturnPath = "/platform/attachments";

    [Fact]
    public async Task Get_WhenTheTokenCacheCannotBeRead_AnswersAServerErrorAndKeepsTheCookie()
    {
        var outage = new TokenCacheOutage();
        await using var root = new ErpApiFactory().WithTestEndpoints(SessionCookies.MapSignInAndObjectId);
        await using var factory = WithOutage(root, outage);
        using var client = TestSignIn.CreateClient(factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        outage.Begin(TokenCacheOperations.All);
        using var response = await SessionCookies.GetObjectIdAsync(client);
        outage.End();
        using var afterwards = await SessionCookies.GetObjectIdAsync(client);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(ProblemTypes.ServerError, problem!.Extensions["code"]?.ToString());
        Assert.Null(SessionCookies.SetCookieOf(response));
        Assert.Equal(HttpStatusCode.OK, afterwards.StatusCode);
    }

    [Fact]
    public async Task Get_WhenOnlyTheSignOutRecordCannotBeRead_AnswersAServerErrorAndKeepsTheCookie()
    {
        var outage = new TokenCacheOutage();
        await using var root = new ErpApiFactory().WithTestEndpoints(SessionCookies.MapSignInAndObjectId);
        await using var factory = WithOutage(root, outage);
        using var client = TestSignIn.CreateClient(factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        outage.Begin(TokenCacheOperations.Read, SessionRevocations.KeyPrefix);
        using var response = await SessionCookies.GetObjectIdAsync(client);
        outage.End();
        using var afterwards = await SessionCookies.GetObjectIdAsync(client);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(ProblemTypes.ServerError, problem!.Extensions["code"]?.ToString());
        Assert.Null(SessionCookies.SetCookieOf(response));
        Assert.Equal(HttpStatusCode.OK, afterwards.StatusCode);
    }

    [Fact]
    public async Task Get_WithACopyOfTheCookieOnceThePersonSignedOutAndSignedInAgainWhileTheirEntryCannotBeRead_RefusesItFromTheSignOutRecordAlone()
    {
        var outage = new TokenCacheOutage();
        await using var root = new ErpApiFactory().WithTestEndpoints(SessionCookies.MapSignInAndObjectId);
        await using var factory = WithOutage(root, outage);
        using var client = TestSignIn.CreateClient(factory);
        using var firstSignIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        var copy = SessionCookies.ValueOf(firstSignIn);
        using var signOut = await client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        using var secondSignIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        outage.Begin(TokenCacheOperations.Read, TestUsers.Accountant.AccountId);
        using var refused = await SessionCookies.GetObjectIdWithAsync(factory, copy);
        outage.End();

        Assert.Equal(HttpStatusCode.Found, signOut.StatusCode);
        Assert.True(await TestSignIn.IsAccountCachedAsync(factory.Services, TestUsers.Accountant));
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Contains(SessionCookies.Cleared, SessionCookies.SetCookieOf(refused), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_LogoutAbandonedByTheBrowserWhileTheSignOutIsRecorded_StillRecordsItAndRefusesACopyOfTheCookie()
    {
        using var browser = new CancellationTokenSource();
        var signOut = new AbandonedSignOut(browser);
        await using var root = new ErpApiFactory().WithTestEndpoints(SessionCookies.MapSignInAndObjectId);
        await using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.DecorateTokenCacheStore(signOut.Wrap)));
        using var client = TestSignIn.CreateClient(factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        var copy = SessionCookies.ValueOf(signIn);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), content: null, browser.Token));
        await signOut.Recorded.WaitAsync(TestContext.Current.CancellationToken);
        using var refused = await SessionCookies.GetObjectIdWithAsync(factory, copy);

        Assert.NotNull(await TokenCacheRow.FindAsync(root.Deployment.TokenCacheKeyPrefix + SessionRevocations.KeyPrefix + TestUsers.Accountant.AccountId));
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Contains(SessionCookies.Cleared, SessionCookies.SetCookieOf(refused), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_LogoutWhenTheSignOutCannotBeRecorded_AnswersAServerErrorWithoutClearingTheCookieAndLeavesThePersonSignedIn()
    {
        var outage = new TokenCacheOutage();
        await using var root = new ErpApiFactory().WithTestEndpoints(SessionCookies.MapSignInAndObjectId);
        await using var factory = WithOutage(root, outage);
        using var client = TestSignIn.CreateClient(factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        outage.Begin(TokenCacheOperations.Write, SessionRevocations.KeyPrefix);
        using var response = await client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        outage.End();
        using var stillSignedIn = await SessionCookies.GetObjectIdAsync(client);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Null(SessionCookies.SetCookieOf(response));
        Assert.Equal(HttpStatusCode.OK, stillSignedIn.StatusCode);
        Assert.True(await TestSignIn.IsAccountCachedAsync(factory.Services, TestUsers.Accountant));
        Assert.Null(await TokenCacheRow.FindAsync(root.Deployment.TokenCacheKeyPrefix + SessionRevocations.KeyPrefix + TestUsers.Accountant.AccountId));
    }

    [Fact]
    public async Task Post_LogoutWhenTheTokenCacheEntryCannotBeRemoved_AnswersAServerErrorAndTheRecordedSignOutStillEndsTheSession()
    {
        var outage = new TokenCacheOutage();
        await using var root = new ErpApiFactory().WithTestEndpoints(SessionCookies.MapSignInAndObjectId);
        await using var factory = WithOutage(root, outage);
        using var client = TestSignIn.CreateClient(factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        outage.Begin(TokenCacheOperations.Remove);
        using var response = await client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        outage.End();
        using var afterwards = await SessionCookies.GetObjectIdAsync(client);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Null(SessionCookies.SetCookieOf(response));
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
        Assert.Contains(SessionCookies.Cleared, SessionCookies.SetCookieOf(afterwards), StringComparison.OrdinalIgnoreCase);
        Assert.True(await TestSignIn.IsAccountCachedAsync(factory.Services, TestUsers.Accountant));
    }

    [Fact]
    public async Task Post_CallbackWhenTheTokenCacheCannotBeReached_LandsOnTheSignInFailedPageWithoutASession()
    {
        var outage = new TokenCacheOutage();
        await using var root = new ErpApiFactory();
        await using var factory = WithOutageAndLogs(root, outage);
        using var client = TestSignIn.CreateClient(factory);

        using var response = await CompleteCallbackAsync(client, TestUsers.Accountant, outage, TokenCacheOperations.All);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(AuthPaths.SignInFailedPage, response.Headers.Location?.OriginalString);
        Assert.Null(SessionCookies.SetCookieOf(response));
        Assert.False(await TestSignIn.IsAccountCachedAsync(factory.Services, TestUsers.Accountant));
        AssertTokenCacheFailureLogged(factory);
    }

    [Fact]
    public async Task Post_CallbackWhenOnlyReadingThePersonsEntryFails_LandsOnTheSignInFailedPageAndKeepsTheirOtherSessions()
    {
        var outage = new TokenCacheOutage();
        await using var root = new ErpApiFactory().WithTestEndpoints(SessionCookies.MapSignInAndObjectId);
        await using var factory = WithOutageAndLogs(root, outage);
        using var signedInDevice = TestSignIn.CreateClient(factory);
        using var signIn = await TestSignIn.SignInAsync(signedInDevice, TestUsers.Accountant);
        using var otherDevice = TestSignIn.CreateClient(factory);

        // The callback request starts with MSAL's in-memory cache empty, so once the read fails the sign-in reports a cache
        // without tokens, which the adapter would answer by deleting the entry the other session needs.
        using var response = await CompleteCallbackAsync(otherDevice, TestUsers.Accountant, outage, TokenCacheOperations.Read);
        using var otherSession = await SessionCookies.GetObjectIdAsync(signedInDevice);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(AuthPaths.SignInFailedPage, response.Headers.Location?.OriginalString);
        Assert.Null(SessionCookies.SetCookieOf(response));
        AssertTokenCacheFailureLogged(factory);
        Assert.Equal(HttpStatusCode.OK, otherSession.StatusCode);
        Assert.True(await TestSignIn.IsAccountCachedAsync(factory.Services, TestUsers.Accountant));
    }

    [Fact]
    public async Task Post_CallbackWhenTheEntraSessionCannotBeRecorded_AnswersAServerErrorWithoutASession()
    {
        var outage = new TokenCacheOutage();
        await using var root = new ErpApiFactory();
        await using var factory = WithOutage(root, outage);
        using var client = TestSignIn.CreateClient(factory);

        using var response = await CompleteCallbackAsync(client, TestUsers.Accountant, outage, TokenCacheOperations.Write, EntraSessions.KeyPrefix);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(ProblemTypes.ServerError, problem!.Extensions["code"]?.ToString());
        Assert.Null(SessionCookies.SetCookieOf(response));
        Assert.Null(await TokenCacheRow.FindAsync(root.Deployment.TokenCacheKeyPrefix + EntraSessions.KeyPrefix + TestUsers.Accountant.EntraSessionId));
    }

    [Fact]
    public async Task Get_FrontChannelSignOutWhenTheSignOutCannotBeRecorded_AnswersAServerErrorAndLeavesThePersonSignedIn()
    {
        var outage = new TokenCacheOutage();
        await using var root = new ErpApiFactory().WithTestEndpoints(SessionCookies.MapSignInAndObjectId);
        await using var factory = WithOutage(root, outage);
        using var client = TestSignIn.CreateClient(factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        outage.Begin(TokenCacheOperations.Write, SessionRevocations.KeyPrefix);
        using var response = await SignOutFromEntraAsync(factory, TestUsers.Accountant);
        outage.End();
        using var stillSignedIn = await SessionCookies.GetObjectIdAsync(client);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, stillSignedIn.StatusCode);
        Assert.True(await TestSignIn.IsAccountCachedAsync(factory.Services, TestUsers.Accountant));
        Assert.NotNull(await TokenCacheRow.FindAsync(root.Deployment.TokenCacheKeyPrefix + EntraSessions.KeyPrefix + TestUsers.Accountant.EntraSessionId));
    }

    [Fact]
    public async Task Get_FrontChannelSignOutWhenTheTokenCacheEntryCannotBeRemoved_AnswersAServerErrorAndTheRecordedSignOutStillEndsTheSession()
    {
        var outage = new TokenCacheOutage();
        await using var root = new ErpApiFactory().WithTestEndpoints(SessionCookies.MapSignInAndObjectId);
        await using var factory = WithOutage(root, outage);
        using var client = TestSignIn.CreateClient(factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        outage.Begin(TokenCacheOperations.Remove);
        using var response = await SignOutFromEntraAsync(factory, TestUsers.Accountant);
        outage.End();
        using var afterwards = await SessionCookies.GetObjectIdAsync(client);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
        Assert.Contains(SessionCookies.Cleared, SessionCookies.SetCookieOf(afterwards), StringComparison.OrdinalIgnoreCase);
        Assert.True(await TestSignIn.IsAccountCachedAsync(factory.Services, TestUsers.Accountant));
    }

    private static async Task<HttpResponseMessage> SignOutFromEntraAsync(WebApplicationFactory<Program> factory, TestUser user)
    {
        using var entra = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

        return await entra.GetAsync(
            new Uri($"{AuthPaths.FrontChannelSignOut}?iss={Uri.EscapeDataString(TestIdentityProvider.Issuer)}&sid={Uri.EscapeDataString(user.EntraSessionId)}", UriKind.Relative),
            TestContext.Current.CancellationToken);
    }

    private static WebApplicationFactory<Program> WithOutage(ErpApiFactory root, TokenCacheOutage outage) =>
        root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.DecorateTokenCacheStore(outage.Wrap)));

    private static WebApplicationFactory<Program> WithOutageAndLogs(ErpApiFactory root, TokenCacheOutage outage) =>
        root.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddFakeLogging();
            services.DecorateTokenCacheStore(outage.Wrap);
        }));

    private static async Task<HttpResponseMessage> CompleteCallbackAsync(HttpClient client, TestUser user, TokenCacheOutage outage, TokenCacheOperations failing, string? keyPrefix = null)
    {
        using var challenge = await client.GetAsync(new Uri($"{AuthPaths.Login}?returnUrl={Uri.EscapeDataString(ReturnPath)}", UriKind.Relative), TestContext.Current.CancellationToken);
        var state = Assert.Single(QueryHelpers.ParseQuery(challenge.Headers.Location!.Query)["state"]);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["state"] = state!,
            ["code"] = TestTokenEndpoint.CodeFor(user),
            ["client_info"] = TestTokenEndpoint.ClientInfoFor(user),
        });

        outage.Begin(failing, keyPrefix);
        try
        {
            return await client.PostAsync(new Uri(AuthPaths.SignInCallback, UriKind.Relative), form, TestContext.Current.CancellationToken);
        }
        finally
        {
            outage.End();
        }
    }

    private static void AssertTokenCacheFailureLogged(WebApplicationFactory<Program> factory)
    {
        var failure = Assert.Single(factory.Services.GetRequiredService<FakeLogCollector>().GetSnapshot(), record => record.Category == typeof(SignInEvents).FullName);
        Assert.Equal(SignInEvents.TokenCacheFailure, failure.GetStructuredStateValue("Failure"));
        Assert.Equal(SignInEvents.NoOAuthError, failure.GetStructuredStateValue("OAuthError"));
    }
}
