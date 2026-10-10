using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.Host.Api.IntegrationTests.Authentication;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authentication.BearerTokens;

namespace Dewiride.Erp.Host.Api.IntegrationTests.BearerTokens;

public sealed class BearerTokenSignInTests(BearerTokenSignInTests.Fixture fixture) : IClassFixture<BearerTokenSignInTests.Fixture>
{
    private const string OtherTenantId = "9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c6d";

    private const string NotAnAccessToken = BearerTokenEvents.NotAnAccessTokenRefusal;

    private static readonly Guid NativeClient = TestApplications.NativeClient.ClientId;

    private static readonly Dictionary<string, RefusedToken> Refused = new(StringComparer.Ordinal)
    {
        ["expired"] = new(() => TestTokenIssuer.Issue(Person(), issuedAt: TimeProvider.System.GetUtcNow().AddHours(-2), lifetime: TimeSpan.FromHours(1)), "expired", NativeClient),
        ["not valid yet"] = new(() => TestTokenIssuer.Issue(Person(), issuedAt: TimeProvider.System.GetUtcNow().AddHours(1)), "not-yet-valid", NativeClient),
        ["for the api:// identifier of the registration"] = new(() => TestTokenIssuer.Issue(Person(("aud", $"api://{TestIdentityProvider.ClientId}"))), "invalid-audience", NativeClient),
        ["for another application"] = new(() => TestTokenIssuer.Issue(Person(("aud", TestApplications.Integration.ClientId.ToString("D")))), "invalid-audience", NativeClient),
        ["issued in another tenant"] = new(() => TestTokenIssuer.Issue(Person(("iss", $"{TestIdentityProvider.Instance}{OtherTenantId}/v2.0"), ("tid", OtherTenantId))), "invalid-issuer", NativeClient),
        ["from the v1.0 endpoint of the tenant"] = new(() => TestTokenIssuer.Issue(Person(("iss", TestTokenIssuer.V1Issuer), ("aud", $"api://{TestIdentityProvider.ClientId}"), ("ver", "1.0"))), "invalid-audience", NativeClient),
        ["naming another tenant under the tenant's issuer"] = new(() => TestTokenIssuer.Issue(Person(("tid", OtherTenantId))), NotAnAccessToken, NativeClient, TestUsers.Accountant.ObjectId),
        ["of version 1.0 under the v2.0 issuer"] = new(() => TestTokenIssuer.Issue(Person(("ver", "1.0"))), NotAnAccessToken, NativeClient, TestUsers.Accountant.ObjectId),
        ["with an object id that is not a GUID"] = new(() => TestTokenIssuer.Issue(Person(("oid", "not-a-guid"))), NotAnAccessToken, NativeClient),
        ["without an object id"] = new(() => TestTokenIssuer.Issue(Without(TestTokenIssuer.PersonClaims(TestUsers.Accountant, TestApplications.NativeClient, [BearerTokenRoutes.ReadScope]), "oid")), NotAnAccessToken, NativeClient),
        ["an id token of the sign-in"] = new(() => TestTokenIssuer.Issue(Without(TestTokenIssuer.PersonClaims(TestUsers.Accountant, TestApplications.NativeClient, []), "scp", "azp", "azpacr")), NotAnAccessToken, null, TestUsers.Accountant.ObjectId),
        ["of an application carrying scopes"] = new(() => TestTokenIssuer.Issue(Application(("scp", BearerTokenRoutes.ReadScope))), NotAnAccessToken, TestApplications.Integration.ClientId, TestApplications.Integration.ObjectId),
        ["unsigned"] = new(() => TestTokenIssuer.IssueUnsigned(Person()), "invalid-signature", NativeClient),
        ["signed with an unknown key"] = new(() => TestTokenIssuer.Issue(Person(), signedWith: TestTokenIssuer.CreateSigningCredentials("erp-test-unknown-key")), "unknown-signing-key", NativeClient),
        ["not a token at all"] = new(() => "not-a-token", "malformed", null),
    };

    public static TheoryData<string> RefusedTokens => [.. Refused.Keys];

    [Fact]
    public async Task Get_PersonTokenWithTheScope_ReachesTheRouteAsThatPerson()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, BearerTokenRoutes.Path, BearerTokenRoutes.PersonToken(BearerTokenRoutes.ReadScope));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new BearerTokenRoutes.ObservedActor(TestUsers.Accountant.ObjectId, true), await response.Content.ReadFromJsonAsync<BearerTokenRoutes.ObservedActor>(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Get_ApplicationTokenWithTheRole_ReachesTheRouteAsThatApplication()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, BearerTokenRoutes.Path, BearerTokenRoutes.ApplicationToken(BearerTokenRoutes.IntegrationRole));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new BearerTokenRoutes.ObservedActor(TestApplications.Integration.ObjectId, true), await response.Content.ReadFromJsonAsync<BearerTokenRoutes.ObservedActor>(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Get_WithoutAToken_AnswersUnauthenticatedNamingTheBearerScheme()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, BearerTokenRoutes.Path, token: null);

        await AssertUnauthenticatedAsync(response, "Bearer");
    }

    [Fact]
    public async Task Get_WithAnotherAuthorizationScheme_AnswersUnauthenticatedNamingTheBearerScheme()
    {
        using var client = fixture.Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, BearerTokenRoutes.Path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", "dXNlcjpwYXNzd29yZA==");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertUnauthenticatedAsync(response, "Bearer");
    }

    [Theory]
    [MemberData(nameof(RefusedTokens))]
    public async Task Get_RefusedToken_AnswersUnauthenticatedNamingAnInvalidTokenWithoutTheReason(string token)
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, BearerTokenRoutes.Path, Refused[token].Issue());

        await AssertUnauthenticatedAsync(response, "Bearer error=\"invalid_token\"");
    }

    [Theory]
    [MemberData(nameof(RefusedTokens))]
    public async Task Get_RefusedToken_IsRecordedByWhatWasWrongWithItAndTheClientApplicationItNames(string token)
    {
        var refused = Refused[token];
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, BearerTokenRoutes.Path, refused.Issue());

        var entry = Assert.Single(await SecurityEventRecords.OfAsync(fixture.Factory.Services, response));
        Assert.Equal(
            (SecurityEventKind.BearerTokenRefused, refused.Refusal, refused.ClientApplication, refused.Holder),
            (entry.Kind, entry.Detail, entry.ClientApplicationId, entry.ActorObjectId));
    }

    [Fact]
    public async Task Get_AcceptedToken_RecordsNoSecurityEvent()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, BearerTokenRoutes.Path, BearerTokenRoutes.PersonToken(BearerTokenRoutes.ReadScope));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await SecurityEventRecords.OfAsync(fixture.Factory.Services, response));
    }

    [Fact]
    public async Task Get_RefusedTokenWhileTheFlagIsOff_RecordsNoSecurityEvent()
    {
        await using var factory = BearerTokenRoutes.Factory(bearerTokensEnabled: false);
        using var client = factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, BearerTokenRoutes.Path, Refused["expired"].Issue());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await SecurityEventRecords.OfAsync(factory.Services, response));
    }

    [Fact]
    public async Task Get_ValidTokenWhileTheFlagIsOff_AnswersUnauthenticatedWithoutAChallenge()
    {
        await using var factory = BearerTokenRoutes.Factory(bearerTokensEnabled: false);
        using var client = factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, BearerTokenRoutes.Path, BearerTokenRoutes.PersonToken(BearerTokenRoutes.ReadScope));

        await AssertUnauthenticatedAsync(response, challenge: null);
    }

    private static async Task AssertUnauthenticatedAsync(HttpResponseMessage response, string? challenge)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal(challenge is null ? [] : [challenge], response.Headers.WwwAuthenticate.Select(value => value.ToString()));
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(ProblemTypes.RequestUnauthenticated, body.RootElement.GetProperty("code").GetString());
        Assert.Equal(AuthenticationProblems.UnauthenticatedTitle, body.RootElement.GetProperty("title").GetString());
    }

    private static Dictionary<string, object> Person(params (string Name, object Value)[] overrides) =>
        With(TestTokenIssuer.PersonClaims(TestUsers.Accountant, TestApplications.NativeClient, [BearerTokenRoutes.ReadScope]), overrides);

    private static Dictionary<string, object> Application(params (string Name, object Value)[] overrides) =>
        With(TestTokenIssuer.ApplicationClaims(TestApplications.Integration, [BearerTokenRoutes.IntegrationRole]), overrides);

    private static Dictionary<string, object> With(Dictionary<string, object> claims, (string Name, object Value)[] overrides)
    {
        foreach (var (name, value) in overrides)
        {
            claims[name] = value;
        }

        return claims;
    }

    private static Dictionary<string, object> Without(Dictionary<string, object> claims, params string[] names)
    {
        foreach (var name in names)
        {
            claims.Remove(name);
        }

        return claims;
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = BearerTokenRoutes.Factory();

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }

    private sealed record RefusedToken(Func<string> Issue, string Refusal, Guid? ClientApplication, Guid? Holder = null);
}
