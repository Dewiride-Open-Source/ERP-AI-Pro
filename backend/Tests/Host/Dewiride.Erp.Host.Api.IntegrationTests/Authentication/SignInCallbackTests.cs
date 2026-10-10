using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Authentication.Antiforgery;
using Dewiride.Erp.BuildingBlocks.Authentication.OpenIdConnect;
using Dewiride.Erp.BuildingBlocks.Authentication.SecurityEvents;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.Host.Api.IntegrationTests.Admission;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Authentication;

public sealed class SignInCallbackTests : IClassFixture<SignInCallbackTests.Fixture>
{
    private const string SessionCookie = "__Host-erp-session";

    private const string Description = "AADSTS65004: User declined to consent to access the app.";

    private const string RefusedCode = "code-that-entra-refuses";

    private const string ReturnPath = "/platform/attachments";

    private const string AccountPath = "/__test/account";

    private const string ChangesPath = "/__test/changes";

    private const string ClearedCookie = "expires=Thu, 01 Jan 1970";

    private readonly Fixture _fixture;

    public SignInCallbackTests(Fixture fixture)
    {
        _fixture = fixture;
        _fixture.Logs.Clear();
    }

    [Fact]
    public async Task Post_CallbackWithTheCodeAndClientInfoOfThePerson_IssuesASessionTheAccountCheckAccepts()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        var state = Single(await ChallengeAsync(client), "state");

        using var response = await PostCallbackAsync(
            client,
            ("state", state),
            ("code", TestTokenEndpoint.CodeFor(TestUsers.Accountant)),
            ("client_info", TestTokenEndpoint.ClientInfoFor(TestUsers.Accountant)));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(ReturnPath, response.Headers.Location?.OriginalString);
        using var browser = TestSignIn.CreateClient(_fixture.Factory);
        using var signedIn = await GetAccountWithOnlyTheSessionCookieAsync(browser, SessionCookieValueOf(response));
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Equal(TestUsers.Accountant.AccountId, await signedIn.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain(_fixture.Logs.GetSnapshot(), record => record.Category == typeof(SignInEvents).FullName);
    }

    [Fact]
    public async Task Post_CallbackWithTheCodeAndClientInfoOfThePerson_RecordsTheSignInOfThePerson()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        var state = Single(await ChallengeAsync(client), "state");

        using var response = await PostCallbackAsync(
            client,
            ("state", state),
            ("code", TestTokenEndpoint.CodeFor(TestUsers.Accountant)),
            ("client_info", TestTokenEndpoint.ClientInfoFor(TestUsers.Accountant)));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var entry = Assert.Single(await SecurityEventRecords.OfAsync(_fixture.Factory.Services, response));
        Assert.Equal((SecurityEventKind.SignedIn, null, TestUsers.Accountant.ObjectId, null), (entry.Kind, entry.Detail, entry.ActorObjectId, entry.ClientApplicationId));
    }

    [Fact]
    public async Task Post_CallbackOfAPersonWithoutARecord_CreatesTheirActiveRecordFromTheIdTokenAsTheirOwn()
    {
        var person = PersonRecords.NewPerson(_fixture.Factory);
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        var state = Single(await ChallengeAsync(client), "state");

        using var response = await PostCallbackAsync(
            client,
            ("state", state),
            ("code", TestTokenEndpoint.CodeFor(person)),
            ("client_info", TestTokenEndpoint.ClientInfoFor(person)));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(ReturnPath, response.Headers.Location?.OriginalString);
        var record = await PersonRecords.OfAsync(_fixture.Factory, person);
        Assert.NotNull(record);
        Assert.Equal((person.Name, person.UserName, true, person.ObjectId), (record.DisplayName, record.WorkEmail, record.IsActive, record.CreatedBy));
        Assert.NotNull(record.LastSignedInAt);
    }

    [Fact]
    public async Task Post_CallbackOfADeactivatedPerson_LandsOnTheAccountDeactivatedPageWithoutASessionAndRecordsTheRefusal()
    {
        var person = PersonRecords.NewPerson(_fixture.Factory);
        await PersonRecords.RegisterAsync(_fixture.Factory, person);
        await PersonRecords.DeactivateAsync(_fixture.Factory, person);
        _fixture.Logs.Clear();
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        var state = Single(await ChallengeAsync(client), "state");

        using var response = await PostCallbackAsync(
            client,
            ("state", state),
            ("code", TestTokenEndpoint.CodeFor(person)),
            ("client_info", TestTokenEndpoint.ClientInfoFor(person)));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(AuthPaths.AccountDeactivatedPage, response.Headers.Location?.OriginalString);
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
        Assert.DoesNotContain(cookies, cookie => cookie.StartsWith($"{SessionCookie}=", StringComparison.Ordinal) && !cookie.Contains(ClearedCookie, StringComparison.OrdinalIgnoreCase));
        var entry = Assert.Single(await SecurityEventRecords.OfAsync(_fixture.Factory.Services, response));
        Assert.Equal(
            (SecurityEventKind.SignInRefused, SignInEvents.DeactivatedRefusal, person.ObjectId, null),
            (entry.Kind, entry.Detail, entry.ActorObjectId, entry.ClientApplicationId));
        var record = Assert.Single(_fixture.Logs.GetSnapshot(), log => log.Category == typeof(SignInEvents).FullName);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal(SignInEvents.DeactivatedRefusal, record.GetStructuredStateValue("Refusal"));
    }

    [Fact]
    public async Task Post_CallbackWithTheCodeAndClientInfoOfThePerson_IssuesAntiforgeryTokensThatCarryThePersonsChanges()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        var state = Single(await ChallengeAsync(client), "state");

        using var callback = await PostCallbackAsync(
            client,
            ("state", state),
            ("code", TestTokenEndpoint.CodeFor(TestUsers.Accountant)),
            ("client_info", TestTokenEndpoint.ClientInfoFor(TestUsers.Accountant)));
        using var change = await client.PostAsync(new Uri(ChangesPath, UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, callback.StatusCode);
        var cookies = callback.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(cookies, cookie => cookie.StartsWith($"{AntiforgeryTokens.CookieName}=", StringComparison.Ordinal));
        Assert.Contains(cookies, cookie => cookie.StartsWith($"{AntiforgeryTokens.RequestTokenCookieName}=", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
    }

    [Fact]
    public async Task Post_CallbackWithTheCodeAndClientInfoOfThePerson_RecordsTheEntraSessionThatTheFrontChannelSignOutEnds()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        var state = Single(await ChallengeAsync(client), "state");
        using var callback = await PostCallbackAsync(
            client,
            ("state", state),
            ("code", TestTokenEndpoint.CodeFor(TestUsers.Administrator)),
            ("client_info", TestTokenEndpoint.ClientInfoFor(TestUsers.Administrator)));
        using var browser = TestSignIn.CreateClient(_fixture.Factory);
        using (var signedIn = await GetAccountWithOnlyTheSessionCookieAsync(browser, SessionCookieValueOf(callback)))
        {
            Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        }

        using var entra = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        using var signOut = await entra.GetAsync(
            new Uri($"{AuthPaths.FrontChannelSignOut}?iss={Uri.EscapeDataString(TestIdentityProvider.Issuer)}&sid={Uri.EscapeDataString(TestUsers.Administrator.EntraSessionId)}", UriKind.Relative),
            TestContext.Current.CancellationToken);
        using var refused = await GetAccountWithOnlyTheSessionCookieAsync(browser, SessionCookieValueOf(callback));

        Assert.Equal(HttpStatusCode.OK, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.True(await TestSignIn.IsAccountCachedAsync(_fixture.Factory.Services, TestUsers.Administrator));
    }

    [Fact]
    public async Task Post_LogoutAfterACompletedCallback_SendsTheLoginHintOfTheIdTokenAsTheLogoutHint()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        var state = Single(await ChallengeAsync(client), "state");
        using var callback = await PostCallbackAsync(
            client,
            ("state", state),
            ("code", TestTokenEndpoint.CodeFor(TestUsers.Administrator)),
            ("client_info", TestTokenEndpoint.ClientInfoFor(TestUsers.Administrator)));

        using var logout = await client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, callback.StatusCode);
        Assert.Equal(HttpStatusCode.Found, logout.StatusCode);
        Assert.Equal(TestIdentityProvider.EndSessionEndpoint.AbsoluteUri, logout.Headers.Location?.GetLeftPart(UriPartial.Path));
        Assert.Equal(TestUsers.Administrator.LoginHint, Single(QueryHelpers.ParseQuery(logout.Headers.Location!.Query), SignInEvents.LogoutHintParameter));
    }

    [Fact]
    public async Task Post_CallbackWithoutClientInfo_IssuesASessionTheNextRequestRefusesAndClears()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        var state = Single(await ChallengeAsync(client), "state");

        using var response = await PostCallbackAsync(client, ("state", state), ("code", TestTokenEndpoint.CodeFor(TestUsers.Accountant)));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(ReturnPath, response.Headers.Location?.OriginalString);
        Assert.True(await TestSignIn.IsAccountCachedAsync(_fixture.Factory.Services, TestUsers.Accountant));
        using var browser = TestSignIn.CreateClient(_fixture.Factory);
        using var refused = await GetAccountWithOnlyTheSessionCookieAsync(browser, SessionCookieValueOf(response));
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        var cleared = Assert.Single(refused.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith($"{SessionCookie}=", StringComparison.Ordinal));
        Assert.Contains(ClearedCookie, cleared, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_CallbackWithoutState_LandsOnTheSignInFailedPageWithoutASession()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);

        using var response = await PostCallbackAsync(client, ("code", "stolen-code"));

        AssertSignInFailed(response);
        AssertLogged(_fixture.Logs.GetSnapshot(), SignInEvents.CallbackFailure, SignInEvents.NoOAuthError);
    }

    [Fact]
    public async Task Post_CallbackWithAForgedState_LandsOnTheSignInFailedPageWithoutASession()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);

        using var response = await PostCallbackAsync(client, ("state", "forged-state"), ("code", "stolen-code"));

        AssertSignInFailed(response);
        AssertLogged(_fixture.Logs.GetSnapshot(), SignInEvents.CallbackFailure, SignInEvents.NoOAuthError);
    }

    [Fact]
    public async Task Post_CallbackWithAValidStateButNoCorrelationCookie_LandsOnTheSignInFailedPage()
    {
        using var challenger = TestSignIn.CreateClient(_fixture.Factory);
        var authorize = await ChallengeAsync(challenger);
        using var client = TestSignIn.CreateClient(_fixture.Factory);

        using var response = await PostCallbackAsync(client, ("state", Single(authorize, "state")), ("code", "stolen-code"));

        AssertSignInFailed(response);
        AssertLogged(_fixture.Logs.GetSnapshot(), SignInEvents.CallbackFailure, SignInEvents.NoOAuthError);
    }

    [Fact]
    public async Task Post_CallbackWhereThePersonDeclined_LandsOnTheSignInFailedPageAndLogsOnlyTheErrorCode()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        var state = Single(await ChallengeAsync(client), "state");

        using var response = await PostCallbackAsync(client, ("state", state), ("error", "access_denied"), ("error_description", Description));

        AssertSignInFailed(response);
        var records = _fixture.Logs.GetSnapshot();
        var record = AssertLogged(records, SignInEvents.IdentityProviderFailure, "access_denied");
        Assert.DoesNotContain("AADSTS65004", record.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(state, record.Message, StringComparison.Ordinal);
        AssertNoRecordContains(records, Description);
    }

    [Fact]
    public async Task Post_CallbackWhereThePersonDeclined_RecordsTheFailedSignInByCategoryAndErrorCodeOnly()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        var state = Single(await ChallengeAsync(client), "state");

        using var response = await PostCallbackAsync(client, ("state", state), ("error", "access_denied"), ("error_description", Description));

        AssertSignInFailed(response);
        var entry = Assert.Single(await SecurityEventRecords.OfAsync(_fixture.Factory.Services, response));
        Assert.Equal((SecurityEventKind.SignInFailed, "identity-provider:access_denied", null), (entry.Kind, entry.Detail, entry.ActorObjectId));
    }

    [Fact]
    public async Task Post_CallbackWithACodeEntraRefuses_RecordsTheFailedSignInByTheErrorCodeOfTheRedemption()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        var state = Single(await ChallengeAsync(client), "state");

        using var response = await PostCallbackAsync(client, ("state", state), ("code", "another-code-that-entra-refuses"));

        AssertSignInFailed(response);
        var entry = Assert.Single(await SecurityEventRecords.OfAsync(_fixture.Factory.Services, response));
        Assert.Equal((SecurityEventKind.SignInFailed, $"{SignInEvents.CodeRedemptionFailure}:{TestTokenEndpoint.RefusedCodeError}"), (entry.Kind, entry.Detail));
    }

    [Fact]
    public async Task Post_FailedCallbacksNamingNobodyOverTheBudget_LeaveOnlyTheBudgetsRecordsInTheMinute()
    {
        await using var factory = new ErpApiFactory();
        using var client = TestSignIn.CreateClientWithoutRequestToken(factory);
        var recorded = new List<int>();

        for (var i = 0; i <= AnonymousSecurityEventBudget.EventsPerWindow; i++)
        {
            using var response = await PostCallbackAsync(client, ("code", "stolen-code"));
            AssertSignInFailed(response);
            recorded.Add((await SecurityEventRecords.OfAsync(factory.Services, response)).Count);
        }

        Assert.Equal(AnonymousSecurityEventBudget.EventsPerWindow, recorded.Sum());
        Assert.Equal(0, recorded[^1]);
    }

    [Fact]
    public async Task Post_CallbackForwardedByATrustedProxy_RecordsTheClientAddressTheProxyNamed()
    {
        await using var factory = new ErpApiFactory()
            .WithConfiguration($"{ErpHostOptions.SectionName}:KnownNetworks:0", "127.0.0.1/32")
            .WithKestrel();
        factory.StartServer();
        using var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { BaseAddress = factory.ClientOptions.BaseAddress };
        using var form = new FormUrlEncodedContent([KeyValuePair.Create("code", "stolen-code")]);
        using var request = new HttpRequestMessage(HttpMethod.Post, AuthPaths.SignInCallback) { Content = form };
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.7");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var entry = Assert.Single(await SecurityEventRecords.OfAsync(factory.Services, response));
        Assert.Equal((SecurityEventKind.SignInFailed, $"{SignInEvents.CallbackFailure}:{SignInEvents.NoOAuthError}", "203.0.113.7"), (entry.Kind, entry.Detail, entry.ClientAddress));
    }

    [Fact]
    public async Task Post_CallbackWithAnErrorOutsideTheOAuthGrammar_LogsItAsUnrecognised()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        var state = Single(await ChallengeAsync(client), "state");

        using var response = await PostCallbackAsync(client, ("state", state), ("error", "<script>alert(1)</script>"));

        AssertSignInFailed(response);
        AssertLogged(_fixture.Logs.GetSnapshot(), SignInEvents.IdentityProviderFailure, SignInEvents.UnrecognisedOAuthError);
    }

    [Fact]
    public async Task Post_CallbackWithACodeEntraRefuses_RedeemsItWithTheCertificateAndPkceAndLandsOnTheSignInFailedPage()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        var authorize = await ChallengeAsync(client);

        using var response = await PostCallbackAsync(client, ("state", Single(authorize, "state")), ("code", RefusedCode));

        AssertSignInFailed(response);
        var redemption = Assert.Single(_fixture.TokenEndpoint.Requests, request => request.Form.GetValueOrDefault("code") == RefusedCode);
        Assert.Equal(TestIdentityProvider.TokenEndpoint.AbsoluteUri, redemption.Address.GetLeftPart(UriPartial.Path));
        Assert.Equal("authorization_code", redemption.Form["grant_type"]);
        Assert.Equal(TestIdentityProvider.ClientId, redemption.Form["client_id"]);
        Assert.Equal($"{TestIdentityProvider.WebOrigin}{AuthPaths.SignInCallback}", redemption.Form["redirect_uri"]);
        Assert.Equal(Single(authorize, "code_challenge"), Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(redemption.Form["code_verifier"]))));
        Assert.Equal("urn:ietf:params:oauth:client-assertion-type:jwt-bearer", redemption.Form["client_assertion_type"]);
        Assert.DoesNotContain("client_secret", redemption.Form.Keys);
        await AssertSignedWithTheSignInCertificateAsync(redemption.Form["client_assertion"]);
        var records = _fixture.Logs.GetSnapshot();
        AssertLogged(records, SignInEvents.CodeRedemptionFailure, TestTokenEndpoint.RefusedCodeError);
        AssertNoRecordContains(records, TestTokenEndpoint.RefusedCodeDescription);
    }

    [Fact]
    public async Task Post_FailedCallbacks_WriteNoErrorDescriptionEvenWhenConfigurationLogsEveryLevelOfTheIdentityLibraries()
    {
        await using var root = new ErpApiFactory()
            .WithConfiguration("Logging:LogLevel:Microsoft.Identity.Web", "Trace")
            .WithConfiguration("Logging:LogLevel:Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectHandler", "Trace");
        await using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddFakeLogging()));
        using var client = TestSignIn.CreateClient(factory);

        using var declined = await PostCallbackAsync(client, ("state", Single(await ChallengeAsync(client), "state")), ("error", "access_denied"), ("error_description", Description));
        using var refused = await PostCallbackAsync(client, ("state", Single(await ChallengeAsync(client), "state")), ("code", RefusedCode));

        AssertSignInFailed(declined);
        AssertSignInFailed(refused);
        var records = factory.Services.GetRequiredService<FakeLogCollector>().GetSnapshot();
        Assert.Contains(records, record => record.Category == typeof(SignInEvents).FullName);
        AssertNoRecordContains(records, Description);
        AssertNoRecordContains(records, TestTokenEndpoint.RefusedCodeDescription);
    }

    private static async Task<Dictionary<string, StringValues>> ChallengeAsync(HttpClient client)
    {
        using var response = await client.GetAsync(new Uri($"{AuthPaths.Login}?returnUrl={Uri.EscapeDataString(ReturnPath)}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        return QueryHelpers.ParseQuery(response.Headers.Location!.Query);
    }

    private static string SessionCookieValueOf(HttpResponseMessage response)
    {
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
        var cookie = Assert.Single(cookies, value => value.StartsWith($"{SessionCookie}=", StringComparison.Ordinal));

        return cookie.Split(';')[0][(SessionCookie.Length + 1)..];
    }

    private static async Task<HttpResponseMessage> GetAccountWithOnlyTheSessionCookieAsync(HttpClient client, string sessionCookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, AccountPath);
        request.Headers.Add("Cookie", $"{SessionCookie}={sessionCookie}");

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string Single(Dictionary<string, StringValues> query, string name) => Assert.Single(query[name])!;

    private static async Task<HttpResponseMessage> PostCallbackAsync(HttpClient client, params (string Name, string Value)[] fields)
    {
        using var form = new FormUrlEncodedContent(fields.Select(field => KeyValuePair.Create(field.Name, field.Value)));

        return await client.PostAsync(new Uri(AuthPaths.SignInCallback, UriKind.Relative), form, TestContext.Current.CancellationToken);
    }

    private static void AssertSignInFailed(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(AuthPaths.SignInFailedPage, response.Headers.Location?.OriginalString);
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
        Assert.DoesNotContain(cookies, cookie => cookie.StartsWith($"{SessionCookie}=", StringComparison.Ordinal));
    }

    private static FakeLogRecord AssertLogged(IReadOnlyList<FakeLogRecord> records, string failure, string oAuthError)
    {
        var record = Assert.Single(records, entry => entry.Category == typeof(SignInEvents).FullName);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal(failure, record.GetStructuredStateValue("Failure"));
        Assert.Equal(oAuthError, record.GetStructuredStateValue("OAuthError"));

        return record;
    }

    private static void AssertNoRecordContains(IReadOnlyList<FakeLogRecord> records, string text)
    {
        var leaks = records
            .Where(record => record.Message.Contains(text, StringComparison.Ordinal)
                || (record.Exception?.ToString().Contains(text, StringComparison.Ordinal) ?? false)
                || (record.StructuredState?.Any(pair => pair.Value?.Contains(text, StringComparison.Ordinal) ?? false) ?? false))
            .Select(record => $"{record.Category} ({record.Level})")
            .ToList();

        Assert.Empty(leaks);
    }

    private static async Task AssertSignedWithTheSignInCertificateAsync(string assertion)
    {
        using var certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(TestSignInCertificate.Base64), password: null);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(assertion, new TokenValidationParameters
        {
            IssuerSigningKey = new X509SecurityKey(certificate),
            ValidIssuer = TestIdentityProvider.ClientId,
            ValidAudience = TestIdentityProvider.TokenEndpoint.AbsoluteUri,
            ValidateIssuerSigningKey = true,
        });

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(TestIdentityProvider.ClientId, result.Claims["sub"]);
    }

    public sealed class Fixture : IDisposable
    {
        private readonly ErpApiFactory _root = new ErpApiFactory().WithTestEndpoints(routes =>
        {
            routes.MapGet(AccountPath, (HttpContext context) => $"{context.User.FindFirst(TestUser.HomeObjectIdClaim)?.Value}.{context.User.FindFirst(TestUser.HomeTenantIdClaim)?.Value}");
            routes.MapPost(ChangesPath, () => Results.NoContent());
        });

        public Fixture()
        {
            Factory = _root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddFakeLogging()));
            Logs = Factory.Services.GetRequiredService<FakeLogCollector>();
            TokenEndpoint = Factory.Services.GetRequiredService<TestTokenEndpoint>();
        }

        public WebApplicationFactory<Program> Factory { get; }

        public FakeLogCollector Logs { get; }

        public TestTokenEndpoint TokenEndpoint { get; }

        public void Dispose()
        {
            Factory.Dispose();
            _root.Dispose();
        }
    }
}
