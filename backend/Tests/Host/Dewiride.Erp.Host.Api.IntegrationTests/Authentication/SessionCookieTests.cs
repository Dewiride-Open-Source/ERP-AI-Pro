using System.Net;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Authentication;

public sealed class SessionCookieTests
{
    private const string SessionCookie = "__Host-erp-session";

    private const string ObjectIdPath = "/__test/object-id";

    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    [Fact]
    public async Task Post_SignIn_IssuesAHostPrefixedHttpOnlySecureLaxSessionCookieThatExpiresWithTheBrowser()
    {
        using var session = new Session();

        using var response = await session.SignInAsync();

        var cookie = SessionCookieOf(response);
        Assert.NotNull(cookie);
        var attributes = cookie.Split(';', StringSplitOptions.TrimEntries).Skip(1).Select(attribute => attribute.ToLowerInvariant()).ToList();
        Assert.Contains("path=/", attributes);
        Assert.Contains("secure", attributes);
        Assert.Contains("samesite=lax", attributes);
        Assert.Contains("httponly", attributes);
        Assert.DoesNotContain(attributes, attribute => attribute.StartsWith("expires=", StringComparison.Ordinal) || attribute.StartsWith("max-age=", StringComparison.Ordinal));
        Assert.DoesNotContain(attributes, attribute => attribute.StartsWith("domain=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Get_WithTheSessionCookie_IsSignedInAsThatPerson()
    {
        using var session = new Session();
        using var signIn = await session.SignInAsync();

        using var response = await session.GetAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TestUsers.Accountant.ObjectId.ToString("D"), await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Get_AfterMoreThanHalfTheIdleTimeout_RenewsTheCookieSoTheSessionSlides()
    {
        using var session = new Session();
        using var signIn = await session.SignInAsync();

        session.Clock.Advance(IdleTimeout / 2 + TimeSpan.FromMinutes(1));
        using var renewed = await session.GetAsync();
        session.Clock.Advance(IdleTimeout / 2 + TimeSpan.FromMinutes(1));
        using var stillSignedIn = await session.GetAsync();

        Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        Assert.NotNull(SessionCookieOf(renewed));
        Assert.Equal(HttpStatusCode.OK, stillSignedIn.StatusCode);
    }

    [Fact]
    public async Task Get_OnceTheIdleTimeoutHasPassed_AnswersUnauthenticated()
    {
        using var session = new Session();
        using var signIn = await session.SignInAsync();

        session.Clock.Advance(IdleTimeout + TimeSpan.FromSeconds(1));
        using var response = await session.GetAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_OnceTheSessionLifetimeHasPassedWhileActive_AnswersUnauthenticatedAndClearsTheCookie()
    {
        using var session = new Session(lifetime: TimeSpan.FromHours(1));
        using var signIn = await session.SignInAsync();
        for (var minutes = 20; minutes < 60; minutes += 20)
        {
            session.Clock.Advance(TimeSpan.FromMinutes(20));
            using var active = await session.GetAsync();
            Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        }

        session.Clock.Advance(TimeSpan.FromMinutes(20));
        using var response = await session.GetAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("expires=Thu, 01 Jan 1970", SessionCookieOf(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_WithATamperedCookie_AnswersUnauthenticated()
    {
        using var session = new Session();
        using var signIn = await session.SignInAsync();
        var value = SessionCookieOf(signIn)!.Split(';')[0][(SessionCookie.Length + 1)..];
        var tampered = (value[^5] == 'A' ? 'B' : 'A') + value[^4..];
        using var client = TestSignIn.CreateClient(session.Factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, ObjectIdPath);
        request.Headers.Add("Cookie", $"{SessionCookie}={value[..^5]}{tampered}");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string? SessionCookieOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.SingleOrDefault(cookie => cookie.StartsWith($"{SessionCookie}=", StringComparison.Ordinal))
            : null;

    private sealed class Session : IDisposable
    {
        private readonly ErpApiFactory _root;

        private readonly HttpClient _client;

        public Session(TimeSpan? lifetime = null)
        {
            _root = new ErpApiFactory().WithTestEndpoints(routes =>
            {
                TestSignIn.Map(routes);
                routes.MapGet(ObjectIdPath, (HttpContext context) => context.User.FindFirst(TestUser.ObjectIdClaim)?.Value);
            });
            if (lifetime is { } sessionLifetime)
            {
                _root.WithConfiguration(ErpApiFactory.IdentitySessionLifetimeKey, sessionLifetime.ToString("c"));
            }

            Factory = _root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(Clock)));
            _client = TestSignIn.CreateClient(Factory);
        }

        public FakeTimeProvider Clock { get; } = new(TimeProvider.System.GetUtcNow());

        public WebApplicationFactory<Program> Factory { get; }

        public Task<HttpResponseMessage> SignInAsync() => TestSignIn.SignInAsync(_client, TestUsers.Accountant);

        public Task<HttpResponseMessage> GetAsync() => _client.GetAsync(new Uri(ObjectIdPath, UriKind.Relative), TestContext.Current.CancellationToken);

        public void Dispose()
        {
            _client.Dispose();
            Factory.Dispose();
            _root.Dispose();
        }
    }
}
