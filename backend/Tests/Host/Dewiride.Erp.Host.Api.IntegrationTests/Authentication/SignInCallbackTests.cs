using System.Net;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Authentication.OpenIdConnect;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Authentication;

public sealed class SignInCallbackTests : IClassFixture<SignInCallbackTests.Fixture>
{
    private const string SessionCookie = "__Host-erp-session";

    private const string Description = "AADSTS65004: User declined to consent to access the app.";

    private readonly Fixture _fixture;

    public SignInCallbackTests(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Post_CallbackWithoutState_LandsOnTheSignInFailedPageWithoutASession()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);

        using var response = await PostCallbackAsync(client, ("code", "stolen-code"));

        AssertSignInFailed(response);
        AssertLogged(SignInEvents.CallbackFailure, SignInEvents.NoOAuthError);
    }

    [Fact]
    public async Task Post_CallbackWithAForgedState_LandsOnTheSignInFailedPageWithoutASession()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);

        using var response = await PostCallbackAsync(client, ("state", "forged-state"), ("code", "stolen-code"));

        AssertSignInFailed(response);
        AssertLogged(SignInEvents.CallbackFailure, SignInEvents.NoOAuthError);
    }

    [Fact]
    public async Task Post_CallbackWithAValidStateButNoCorrelationCookie_LandsOnTheSignInFailedPage()
    {
        using var challenger = TestSignIn.CreateClient(_fixture.Factory);
        var state = await ChallengeAsync(challenger);
        using var client = TestSignIn.CreateClient(_fixture.Factory);

        using var response = await PostCallbackAsync(client, ("state", state), ("code", "stolen-code"));

        AssertSignInFailed(response);
        AssertLogged(SignInEvents.CallbackFailure, SignInEvents.NoOAuthError);
    }

    [Fact]
    public async Task Post_CallbackWhereThePersonDeclined_LandsOnTheSignInFailedPageAndLogsOnlyTheErrorCode()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        var state = await ChallengeAsync(client);

        using var response = await PostCallbackAsync(client, ("state", state), ("error", "access_denied"), ("error_description", Description));

        AssertSignInFailed(response);
        var record = AssertLogged(SignInEvents.IdentityProviderFailure, "access_denied");
        Assert.DoesNotContain("AADSTS65004", record.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(state, record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_CallbackWithAnErrorOutsideTheOAuthGrammar_LogsItAsUnrecognised()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        var state = await ChallengeAsync(client);

        using var response = await PostCallbackAsync(client, ("state", state), ("error", "<script>alert(1)</script>"));

        AssertSignInFailed(response);
        AssertLogged(SignInEvents.IdentityProviderFailure, SignInEvents.UnrecognisedOAuthError);
    }

    private static async Task<string> ChallengeAsync(HttpClient client)
    {
        using var response = await client.GetAsync(new Uri($"{AuthPaths.Login}?returnUrl=%2F", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        return Assert.Single(QueryHelpers.ParseQuery(response.Headers.Location!.Query)["state"])!;
    }

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

    private FakeLogRecord AssertLogged(string failure, string oAuthError)
    {
        var record = Assert.Single(_fixture.Logs.GetSnapshot(clear: true), entry => entry.Category == typeof(SignInEvents).FullName);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal(failure, record.GetStructuredStateValue("Failure"));
        Assert.Equal(oAuthError, record.GetStructuredStateValue("OAuthError"));

        return record;
    }

    public sealed class Fixture : IDisposable
    {
        private readonly ErpApiFactory _root = new();

        public Fixture()
        {
            Factory = _root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddFakeLogging()));
            Logs = Factory.Services.GetRequiredService<FakeLogCollector>();
        }

        public WebApplicationFactory<Program> Factory { get; }

        public FakeLogCollector Logs { get; }

        public void Dispose()
        {
            Factory.Dispose();
            _root.Dispose();
        }
    }
}
