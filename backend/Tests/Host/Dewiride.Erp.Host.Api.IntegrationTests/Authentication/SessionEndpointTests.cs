using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Authentication.Antiforgery;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Authentication;

public sealed class SessionEndpointTests
{
    private const string SessionCookie = "__Host-erp-session";

    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    [Fact]
    public async Task Get_SignedIn_ReturnsTheIdleExpiryAndTheLifetimeEndWithoutRenewingTheCookie()
    {
        using var session = new Session();
        using var signIn = await session.SignInAsync();

        session.Clock.Advance(TimeSpan.FromMinutes(5));
        using var response = await session.ReadAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(SessionCookieOf(response));
        var (expiresAt, lifetimeEndsAt) = await TimesOfAsync(response);
        Assert.Equal(session.SignedInAt + IdleTimeout, expiresAt);
        Assert.Equal(session.SignedInAt + Lifetime, lifetimeEndsAt);
    }

    [Fact]
    public async Task Get_AfterARequestThatRenewedTheCookie_ReturnsTheRenewedExpiry()
    {
        using var session = new Session();
        using var signIn = await session.SignInAsync();

        session.Clock.Advance(TimeSpan.FromMinutes(2));
        using var renewing = await session.Client.GetAsync(new Uri(AuthPaths.Me, UriKind.Relative), TestContext.Current.CancellationToken);
        using var response = await session.ReadAsync();

        Assert.NotNull(SessionCookieOf(renewing));
        var (expiresAt, _) = await TimesOfAsync(response);
        Assert.Equal(session.SignedInAt + TimeSpan.FromMinutes(2) + IdleTimeout, expiresAt);
    }

    [Fact]
    public async Task Get_AfterTheIdleTimeout_AnswersUnauthenticated()
    {
        using var session = new Session();
        using var signIn = await session.SignInAsync();

        session.Clock.Advance(IdleTimeout + TimeSpan.FromSeconds(1));
        using var response = await session.ReadAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ProblemTypes.RequestUnauthenticated, await CodeOfAsync(response));
    }

    [Fact]
    public async Task Post_SignedIn_RenewsTheCookieAtOnceAndReturnsTheNewExpiry()
    {
        using var session = new Session();
        using var signIn = await session.SignInAsync();

        session.Clock.Advance(TimeSpan.FromSeconds(10));
        using var response = await session.RenewAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(SessionCookieOf(response));
        var (expiresAt, lifetimeEndsAt) = await TimesOfAsync(response);
        Assert.Equal(session.SignedInAt + TimeSpan.FromSeconds(10) + IdleTimeout, expiresAt);
        Assert.Equal(session.SignedInAt + Lifetime, lifetimeEndsAt);
    }

    [Fact]
    public async Task Post_Renewed_KeepsThePersonSignedInForTheIdleTimeoutFromTheRenewal()
    {
        using var session = new Session();
        using var signIn = await session.SignInAsync();

        session.Clock.Advance(TimeSpan.FromMinutes(20));
        using var renewed = await session.RenewAsync();
        session.Clock.Advance(TimeSpan.FromMinutes(29));
        using var response = await session.ReadAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (expiresAt, _) = await TimesOfAsync(response);
        Assert.Equal(session.SignedInAt + TimeSpan.FromMinutes(50), expiresAt);
    }

    [Fact]
    public async Task Post_NearTheEndOfTheLifetime_RenewsOnlyUntilTheLifetimeEnds()
    {
        using var session = new Session(lifetime: "01:00:00");
        using var signIn = await session.SignInAsync();

        session.Clock.Advance(TimeSpan.FromMinutes(20));
        using var first = await session.RenewAsync();
        session.Clock.Advance(TimeSpan.FromMinutes(20));
        using var second = await session.RenewAsync();

        var (expiresAt, lifetimeEndsAt) = await TimesOfAsync(second);
        Assert.Equal(session.SignedInAt + TimeSpan.FromHours(1), lifetimeEndsAt);
        Assert.Equal(lifetimeEndsAt, expiresAt);
    }

    // Every clock reading moves the clock a second: the cookie handler renews from the reading after the one the session
    // events record, and any reading taken later in the request, the session check's included, would give a later expiry.
    [Fact]
    public async Task Post_WhileTheClockMovesDuringTheRequest_NeverReportsTimesLaterThanTheSessionReallyEnds()
    {
        using var session = new Session(afterAWholeSecond: TimeSpan.FromMilliseconds(900));
        using var signIn = await session.SignInAsync();
        session.Clock.Advance(TimeSpan.FromMinutes(2));
        session.Clock.AutoAdvanceAmount = TimeSpan.FromSeconds(1);

        using var renewed = await session.RenewAsync();
        using var read = await session.ReadAsync();

        var (renewedExpiry, lifetimeEndsAt) = await TimesOfAsync(renewed);
        var (cookieExpiry, _) = await TimesOfAsync(read);
        Assert.InRange(cookieExpiry - renewedExpiry, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        Assert.Equal(0, renewedExpiry.Ticks % TimeSpan.TicksPerSecond);
        Assert.Equal(WholeSecondsOf(session.SignedInAt + Lifetime), lifetimeEndsAt);
    }

    [Fact]
    public async Task Post_WithoutTheRequestToken_AnswersTokenMissing()
    {
        using var session = new Session(sendRequestToken: false);
        using var signIn = await session.SignInAsync();

        using var response = await session.RenewAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AntiforgeryProblems.TokenMissing, await CodeOfAsync(response));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task Send_SignedInThroughTheTestHeaderWithoutASession_AnswersUnauthenticated(string method)
    {
        using var session = new Session();
        using var client = session.Factory.CreateClient().AsUser(TestUsers.Accountant);
        using var request = new HttpRequestMessage(new HttpMethod(method), AuthPaths.Session);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ProblemTypes.RequestUnauthenticated, await CodeOfAsync(response));
    }

    private static DateTimeOffset WholeSecondsOf(DateTimeOffset time) => time.AddTicks(-(time.Ticks % TimeSpan.TicksPerSecond));

    private static string? SessionCookieOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.SingleOrDefault(cookie => cookie.StartsWith($"{SessionCookie}=", StringComparison.Ordinal))
            : null;

    private static async Task<(DateTimeOffset ExpiresAt, DateTimeOffset LifetimeEndsAt)> TimesOfAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["expiresAt", "lifetimeEndsAt"], body.RootElement.EnumerateObject().Select(property => property.Name));

        return (body.RootElement.GetProperty("expiresAt").GetDateTimeOffset(), body.RootElement.GetProperty("lifetimeEndsAt").GetDateTimeOffset());
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("code").GetString();
    }

    // The clock starts on a whole second unless a test moves it off one, because the cookie keeps its issue and expiry times
    // to the second.
    private sealed class Session : IDisposable
    {
        private readonly ErpApiFactory _root;

        public Session(string? lifetime = null, bool sendRequestToken = true, TimeSpan afterAWholeSecond = default)
        {
            Clock = new FakeTimeProvider(WholeSecondsOf(TimeProvider.System.GetUtcNow()) + afterAWholeSecond);
            _root = new ErpApiFactory().WithTestEndpoints(TestSignIn.Map);
            if (lifetime is not null)
            {
                _root.WithConfiguration(ErpApiFactory.IdentitySessionLifetimeKey, lifetime);
            }

            Factory = _root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(Clock)));
            Client = sendRequestToken ? TestSignIn.CreateClient(Factory) : TestSignIn.CreateClientWithoutRequestToken(Factory);
        }

        public FakeTimeProvider Clock { get; }

        public WebApplicationFactory<Program> Factory { get; }

        public HttpClient Client { get; }

        public DateTimeOffset SignedInAt { get; private set; }

        public async Task<HttpResponseMessage> SignInAsync()
        {
            SignedInAt = Clock.GetUtcNow();

            return await TestSignIn.SignInAsync(Client, TestUsers.Accountant);
        }

        public Task<HttpResponseMessage> ReadAsync() => Client.GetAsync(new Uri(AuthPaths.Session, UriKind.Relative), TestContext.Current.CancellationToken);

        public Task<HttpResponseMessage> RenewAsync() => Client.PostAsync(new Uri(AuthPaths.Session, UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        public void Dispose()
        {
            Client.Dispose();
            Factory.Dispose();
            _root.Dispose();
        }
    }
}
