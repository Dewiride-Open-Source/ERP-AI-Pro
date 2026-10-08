using System.Net;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Authentication.Endpoints.Requests;
using Dewiride.Erp.BuildingBlocks.Authentication.Sessions;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.Host.Api.IntegrationTests.TokenCache;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Authentication;

public sealed class FrontChannelSignOutTests : IClassFixture<FrontChannelSignOutTests.Fixture>
{
    private const string SignedInPath = "/__test/signed-in";

    private const string OtherIssuer = "https://login.microsoftonline.com/0b1c2d3e-4f5a-4b6c-8d7e-9f0a1b2c3d4e/v2.0";

    private readonly Fixture _fixture;

    public FrontChannelSignOutTests(Fixture fixture)
    {
        _fixture = fixture;
        _fixture.Logs.Clear();
    }

    [Fact]
    public async Task Get_EntraSessionOfASignedInPerson_EndsEverySessionOfThePersonAndRemovesTheirAccount()
    {
        using var laptop = TestSignIn.CreateClient(_fixture.Factory);
        using var phone = TestSignIn.CreateClient(_fixture.Factory);
        using var laptopSignIn = await TestSignIn.SignInAsync(laptop, TestUsers.Accountant);
        using var phoneSignIn = await TestSignIn.SignInAsync(phone, TestUsers.Accountant);
        await AssertStatusAsync(laptop, HttpStatusCode.OK);

        using var response = await SignOutFromEntraAsync(TestIdentityProvider.Issuer, TestUsers.Accountant.EntraSessionId);

        await AssertAnsweredWithoutABodyAsync(response);
        await AssertStatusAsync(laptop, HttpStatusCode.Unauthorized);
        await AssertStatusAsync(phone, HttpStatusCode.Unauthorized);
        Assert.False(await TestSignIn.IsAccountCachedAsync(_fixture.Factory.Services, TestUsers.Accountant));
        Assert.Null(await TokenCacheRow.FindAsync(_fixture.KeyPrefix + EntraSessions.KeyPrefix + TestUsers.Accountant.EntraSessionId));
        Assert.NotNull(await TokenCacheRow.FindAsync(_fixture.KeyPrefix + SessionRevocations.KeyPrefix + TestUsers.Accountant.AccountId));
    }

    [Fact]
    public async Task Get_EntraSessionOfASignedInPerson_LeavesTheSessionsOfOtherPeopleWorking()
    {
        using var accountant = TestSignIn.CreateClient(_fixture.Factory);
        using var administrator = TestSignIn.CreateClient(_fixture.Factory);
        using var accountantSignIn = await TestSignIn.SignInAsync(accountant, TestUsers.Accountant);
        using var administratorSignIn = await TestSignIn.SignInAsync(administrator, TestUsers.Administrator);

        using var response = await SignOutFromEntraAsync(TestIdentityProvider.Issuer, TestUsers.Accountant.EntraSessionId);

        await AssertAnsweredWithoutABodyAsync(response);
        await AssertStatusAsync(accountant, HttpStatusCode.Unauthorized);
        await AssertStatusAsync(administrator, HttpStatusCode.OK);
        Assert.True(await TestSignIn.IsAccountCachedAsync(_fixture.Factory.Services, TestUsers.Administrator));
    }

    [Fact]
    public async Task Get_EntraSessionWithAnotherIssuer_ChangesNothingAndAnswersAlike()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Administrator);

        using var response = await SignOutFromEntraAsync(OtherIssuer, TestUsers.Administrator.EntraSessionId);

        await AssertAnsweredWithoutABodyAsync(response);
        await AssertStatusAsync(client, HttpStatusCode.OK);
        Assert.True(await TestSignIn.IsAccountCachedAsync(_fixture.Factory.Services, TestUsers.Administrator));
        Assert.NotNull(await TokenCacheRow.FindAsync(_fixture.KeyPrefix + EntraSessions.KeyPrefix + TestUsers.Administrator.EntraSessionId));
    }

    [Fact]
    public async Task Get_UnknownEntraSession_ChangesNothingAndAnswersAlike()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Administrator);

        using var response = await SignOutFromEntraAsync(TestIdentityProvider.Issuer, Guid.CreateVersion7().ToString("D"));

        await AssertAnsweredWithoutABodyAsync(response);
        await AssertStatusAsync(client, HttpStatusCode.OK);
        Assert.True(await TestSignIn.IsAccountCachedAsync(_fixture.Factory.Services, TestUsers.Administrator));
    }

    [Fact]
    public async Task Get_EntraSessionOfAPersonWhoAlreadySignedOut_AnswersAlike()
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Administrator);
        using var signOut = await client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Found, signOut.StatusCode);

        using var response = await SignOutFromEntraAsync(TestIdentityProvider.Issuer, TestUsers.Administrator.EntraSessionId);

        await AssertAnsweredWithoutABodyAsync(response);
        Assert.Null(await TokenCacheRow.FindAsync(_fixture.KeyPrefix + EntraSessions.KeyPrefix + TestUsers.Administrator.EntraSessionId));
    }

    [Theory]
    [InlineData("?sid={sid}", "iss")]
    [InlineData("?iss=&sid={sid}", "iss")]
    [InlineData("?iss={long-issuer}&sid={sid}", "iss")]
    [InlineData("?iss={issuer}", "sid")]
    [InlineData("?iss={issuer}&sid=", "sid")]
    [InlineData("?iss={issuer}&sid={long-sid}", "sid")]
    public async Task Get_MissingOrOverLongParameter_AnswersAValidationProblemForItAndChangesNothing(string query, string parameter)
    {
        using var client = TestSignIn.CreateClient(_fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Administrator);
        var path = AuthPaths.FrontChannelSignOut + query
            .Replace("{issuer}", Uri.EscapeDataString(TestIdentityProvider.Issuer), StringComparison.Ordinal)
            .Replace("{long-issuer}", Uri.EscapeDataString(TestIdentityProvider.Issuer.PadRight(FrontChannelSignOutRequest.MaxIssuerLength + 1, 'x')), StringComparison.Ordinal)
            .Replace("{long-sid}", new string('a', FrontChannelSignOutRequest.MaxSessionIdLength + 1), StringComparison.Ordinal)
            .Replace("{sid}", TestUsers.Administrator.EntraSessionId, StringComparison.Ordinal);

        using var entra = EntraFrame();
        using var response = await entra.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await AssertValidationProblemAsync(response);
        Assert.Equal([parameter], problem.Errors.Keys);
        await AssertStatusAsync(client, HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_WithoutParameters_AnswersAValidationProblemForBoth()
    {
        using var entra = EntraFrame();
        using var response = await entra.GetAsync(new Uri(AuthPaths.FrontChannelSignOut, UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await AssertValidationProblemAsync(response);
        Assert.Equal(["iss", "sid"], problem.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Get_LongestAcceptedParameters_AnswersAlike()
    {
        using var response = await SignOutFromEntraAsync(new string('i', FrontChannelSignOutRequest.MaxIssuerLength), new string('s', FrontChannelSignOutRequest.MaxSessionIdLength));

        await AssertAnsweredWithoutABodyAsync(response);
    }

    [Fact]
    public async Task Get_EntraSessionOfASignedInPerson_LogsTheSignOutWithoutTheSessionTheIssuerOrTheAccount()
    {
        using var accountant = TestSignIn.CreateClient(_fixture.Factory);
        using var administrator = TestSignIn.CreateClient(_fixture.Factory);
        using var accountantSignIn = await TestSignIn.SignInAsync(accountant, TestUsers.Accountant);
        using var administratorSignIn = await TestSignIn.SignInAsync(administrator, TestUsers.Administrator);
        _fixture.Logs.Clear();

        using var ended = await SignOutFromEntraAsync(TestIdentityProvider.Issuer, TestUsers.Accountant.EntraSessionId);
        using var otherIssuer = await SignOutFromEntraAsync(OtherIssuer, TestUsers.Administrator.EntraSessionId);

        var records = _fixture.Logs.GetSnapshot();
        var own = records.Where(record => record.Category == typeof(FrontChannelSignOut).FullName).ToList();
        Assert.Contains(own, record => record.Level == LogLevel.Information);
        Assert.Contains(own, record => record.Level == LogLevel.Warning);
        string[] personal = [TestUsers.Accountant.EntraSessionId, TestUsers.Accountant.AccountId, TestUsers.Administrator.EntraSessionId, TestUsers.Administrator.AccountId];
        Assert.DoesNotContain(records, record => personal.Any(value => Carries(record, value)));
        Assert.DoesNotContain(own, record => Carries(record, TestIdentityProvider.Issuer) || Carries(record, OtherIssuer));
    }

    private async Task<HttpResponseMessage> SignOutFromEntraAsync(string issuer, string sessionId)
    {
        using var entra = EntraFrame();

        return await entra.GetAsync(
            new Uri($"{AuthPaths.FrontChannelSignOut}?iss={Uri.EscapeDataString(issuer)}&sid={Uri.EscapeDataString(sessionId)}", UriKind.Relative),
            TestContext.Current.CancellationToken);
    }

    private HttpClient EntraFrame()
    {
        var client = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = TestSignIn.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });
        client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "cross-site");
        client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
        client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "iframe");

        return client;
    }

    private static async Task AssertAnsweredWithoutABodyAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    private static async Task<HttpValidationProblemDetails> AssertValidationProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal(ProblemTypes.RequestInvalid, problem.Extensions["code"]?.ToString());

        return problem;
    }

    private static async Task AssertStatusAsync(HttpClient client, HttpStatusCode status)
    {
        using var response = await client.GetAsync(new Uri(SignedInPath, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(status, response.StatusCode);
    }

    private static bool Carries(FakeLogRecord record, string value) =>
        record.Message.Contains(value, StringComparison.Ordinal)
        || (record.Exception?.ToString().Contains(value, StringComparison.Ordinal) ?? false)
        || (record.StructuredState?.Any(pair => pair.Value?.Contains(value, StringComparison.Ordinal) ?? false) ?? false);

    public sealed class Fixture : IAsyncDisposable
    {
        private readonly ErpApiFactory _root = new ErpApiFactory().WithTestEndpoints(routes =>
        {
            TestSignIn.Map(routes);
            routes.MapGet(SignedInPath, () => Results.Ok());
        });

        public Fixture()
        {
            Factory = _root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddFakeLogging()));
            Logs = Factory.Services.GetRequiredService<FakeLogCollector>();
            KeyPrefix = _root.Deployment.TokenCacheKeyPrefix;
        }

        public WebApplicationFactory<Program> Factory { get; }

        public FakeLogCollector Logs { get; }

        public string KeyPrefix { get; }

        public async ValueTask DisposeAsync()
        {
            await Factory.DisposeAsync();
            await _root.DisposeAsync();
        }
    }
}
