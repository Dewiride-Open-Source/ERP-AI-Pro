using System.Net;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;
using Dewiride.Erp.Host.Api.IntegrationTests.Authentication;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Host.Api.IntegrationTests.RateLimiting;

public sealed class SignInRateLimitingTests
{
    private static readonly Uri LoginPath = new($"{AuthPaths.Login}?returnUrl=%2F", UriKind.Relative);

    [Fact]
    public async Task Get_LoginOverTheSignInLimit_AnswersATooManyRequestsProblemWithRetryAfterAndNoCookie()
    {
        await using var factory = new ErpApiFactory().WithConfiguration(ErpApiFactory.SignInPermitLimitKey, "2");
        using var client = Client(factory);
        await AssertSentToEntraAsync(client, LoginPath, times: 2);

        using var response = await client.GetAsync(LoginPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.InRange(response.Headers.RetryAfter?.Delta ?? TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("/problems/rate-limit.exceeded", problem!.Type);
        Assert.Equal(ProblemTypes.RateLimitExceeded, problem.Extensions["code"]?.ToString());
        Assert.Equal(Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)), problem.Extensions[ProblemTypes.TraceIdExtension]?.ToString());
    }

    [Fact]
    public async Task Post_SignInCallbackOverTheSignInLimit_IsRefusedBeforeTheOpenIdConnectHandlerAnswersIt()
    {
        await using var factory = new ErpApiFactory().WithConfiguration(ErpApiFactory.SignInPermitLimitKey, "1");
        using var client = Client(factory);
        using var answered = await PostCallbackAsync(client);

        using var refused = await PostCallbackAsync(client);

        Assert.Equal(HttpStatusCode.Found, answered.StatusCode);
        Assert.Equal(AuthPaths.SignInFailedPage, answered.Headers.Location?.OriginalString);
        Assert.Single(await SecurityEventRecords.OfAsync(factory.Services, answered));
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Null(refused.Headers.Location);
        Assert.Empty(await SecurityEventRecords.OfAsync(factory.Services, refused));
    }

    [Theory]
    [InlineData("GET", AuthPaths.SignedOutCallback)]
    [InlineData("GET", AuthPaths.FrontChannelSignOut + "?iss=https%3A%2F%2Fissuer.example.com%2F&sid=7c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e4f")]
    [InlineData("POST", AuthPaths.Logout)]
    public async Task Send_SignOutPathOverTheSignInLimit_AnswersATooManyRequestsProblemWithRetryAfter(string method, string path)
    {
        await using var factory = new ErpApiFactory().WithConfiguration(ErpApiFactory.SignInPermitLimitKey, "1");
        using var client = Client(factory);
        using var answered = await SendAsync(client, method, path);

        using var refused = await SendAsync(client, method, path);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, answered.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.InRange(refused.Headers.RetryAfter?.Delta ?? TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
        var problem = await refused.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(ProblemTypes.RateLimitExceeded, problem!.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Send_SignOutOfAnAddressWhoseLoginsSpentTheirAllowance_IsServed()
    {
        await using var factory = new ErpApiFactory().WithConfiguration(ErpApiFactory.SignInPermitLimitKey, "1");
        using var client = Client(factory);
        await AssertSentToEntraAsync(client, LoginPath, times: 1);
        using var refusedLogin = await client.GetAsync(LoginPath, TestContext.Current.CancellationToken);

        using var logout = await SendAsync(client, "POST", AuthPaths.Logout);

        Assert.Equal(HttpStatusCode.TooManyRequests, refusedLogin.StatusCode);
        Assert.Equal(HttpStatusCode.Found, logout.StatusCode);
    }

    [Fact]
    public async Task Get_LoginWithATrailingSlashOrInAnotherCase_StartsTheSignInAndSharesTheAllowance()
    {
        await using var factory = new ErpApiFactory().WithConfiguration(ErpApiFactory.SignInPermitLimitKey, "2");
        using var client = Client(factory);

        await AssertSentToEntraAsync(client, new Uri($"{AuthPaths.Login}/?returnUrl=%2F", UriKind.Relative), times: 1);
        await AssertSentToEntraAsync(client, new Uri($"{AuthPaths.Login.ToUpperInvariant()}?returnUrl=%2F", UriKind.Relative), times: 1);
        using var response = await client.GetAsync(LoginPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Get_OtherRoutesBeyondTheSignInLimit_SpendNoSignInAllowance()
    {
        await using var factory = new ErpApiFactory().WithConfiguration(ErpApiFactory.SignInPermitLimitKey, "1");
        using var client = Client(factory);

        for (var i = 0; i < 3; i++)
        {
            using var health = await client.GetAsync(new Uri("/healthz/live", UriKind.Relative), TestContext.Current.CancellationToken);
            using var me = await client.GetAsync(new Uri(AuthPaths.Me, UriKind.Relative), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        }

        await AssertSentToEntraAsync(client, LoginPath, times: 1);
    }

    [Fact]
    public async Task Get_LoginWithRateLimitingDisabled_IsNeverLimited()
    {
        await using var factory = new ErpApiFactory()
            .WithConfiguration(ErpApiFactory.SignInPermitLimitKey, "1")
            .WithConfiguration($"{RateLimitingOptions.SectionName}:Enabled", "false");
        using var client = Client(factory);

        await AssertSentToEntraAsync(client, LoginPath, times: 3);
    }

    [Fact]
    public async Task Get_SignInCallersForwardedByATrustedProxy_EachHaveTheirOwnAllowance()
    {
        await using var factory = new ErpApiFactory()
            .WithConfiguration(ErpApiFactory.SignInPermitLimitKey, "1")
            .WithConfiguration($"{ErpHostOptions.SectionName}:KnownNetworks:0", "127.0.0.1/32")
            .WithKestrel();
        factory.StartServer();
        using var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { BaseAddress = factory.ClientOptions.BaseAddress };

        Assert.Equal(HttpStatusCode.Found, await LoginStatusForAsync(client, "203.0.113.7"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await LoginStatusForAsync(client, "203.0.113.7"));
        Assert.Equal(HttpStatusCode.Found, await LoginStatusForAsync(client, "203.0.113.8"));
    }

    private static HttpClient Client(ErpApiFactory factory)
    {
        factory.ClientOptions.AllowAutoRedirect = false;

        return factory.CreateClient();
    }

    private static async Task AssertSentToEntraAsync(HttpClient client, Uri path, int times)
    {
        for (var i = 0; i < times; i++)
        {
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Found, response.StatusCode);
            Assert.Equal(TestIdentityProvider.AuthorizationEndpoint.GetLeftPart(UriPartial.Path), response.Headers.Location?.GetLeftPart(UriPartial.Path));
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> PostCallbackAsync(HttpClient client)
    {
        using var form = new FormUrlEncodedContent([KeyValuePair.Create("code", "stolen-code")]);

        return await client.PostAsync(new Uri(AuthPaths.SignInCallback, UriKind.Relative), form, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpStatusCode> LoginStatusForAsync(HttpClient client, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LoginPath);
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        return response.StatusCode;
    }
}
