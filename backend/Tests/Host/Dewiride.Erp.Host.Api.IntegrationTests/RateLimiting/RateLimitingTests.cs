using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;
using Dewiride.Erp.Testing;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Host.Api.IntegrationTests.RateLimiting;

public sealed class RateLimitingTests
{
    private const string SystemInfoFlag = "Erp.Modules.Platform.SystemInfo";

    private const string LimitKey = $"{RateLimitingOptions.SectionName}:AnonymousPermitLimit";

    private static readonly Uri SystemInfoPath = new("/api/platform/system-info", UriKind.Relative);

    [Fact]
    public async Task Get_AnonymousCallerOverTheLimit_AnswersATooManyRequestsProblemWithRetryAfter()
    {
        await using var factory = new ErpApiFactory().WithConfiguration(LimitKey, "2");
        using var client = factory.CreateClient();
        await AssertServedAsync(client, SystemInfoPath, times: 2);

        using var response = await client.GetAsync(SystemInfoPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.InRange(response.Headers.RetryAfter?.Delta ?? TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("/problems/rate-limit.exceeded", problem!.Type);
        Assert.Equal(ProblemTypes.RateLimitExceeded, problem.Extensions["code"]?.ToString());
        Assert.Equal(Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)), problem.Extensions[ProblemTypes.TraceIdExtension]?.ToString());
    }

    [Fact]
    public async Task Get_HealthEndpointsBeyondTheLimit_AreNeverLimitedAndSpendNoAllowance()
    {
        await using var factory = new ErpApiFactory().WithConfiguration(LimitKey, "2");
        using var client = factory.CreateClient();

        await AssertServedAsync(client, new Uri("/healthz/live", UriKind.Relative), times: 5);
        await AssertServedAsync(client, new Uri("/healthz/ready", UriKind.Relative), times: 5);

        await AssertServedAsync(client, SystemInfoPath, times: 2);
    }

    [Fact]
    public async Task Get_WithRateLimitingDisabled_IsNeverLimited()
    {
        await using var factory = new ErpApiFactory()
            .WithConfiguration(LimitKey, "1")
            .WithConfiguration($"{RateLimitingOptions.SectionName}:Enabled", "false");
        using var client = factory.CreateClient();

        await AssertServedAsync(client, SystemInfoPath, times: 5);
    }

    [Fact]
    public async Task Get_ClientsForwardedByATrustedProxy_EachHaveTheirOwnAllowance()
    {
        await using var factory = new ErpApiFactory()
            .WithConfiguration(LimitKey, "2")
            .WithConfiguration($"{ErpHostOptions.SectionName}:KnownNetworks:0", "127.0.0.1/32")
            .WithKestrel();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await StatusForAsync(client, "203.0.113.7"));
        Assert.Equal(HttpStatusCode.OK, await StatusForAsync(client, "203.0.113.7"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await StatusForAsync(client, "203.0.113.7"));
        Assert.Equal(HttpStatusCode.OK, await StatusForAsync(client, "203.0.113.8"));
    }

    [Fact]
    public async Task Get_DisabledModuleBeyondTheLimit_IsLimitedBeforeTheFeatureGateAnswers()
    {
        await using var factory = new ErpApiFactory().WithConfiguration(LimitKey, "1").WithFeature(SystemInfoFlag, enabled: false);
        using var client = factory.CreateClient();

        using var first = await client.GetAsync(SystemInfoPath, TestContext.Current.CancellationToken);
        using var second = await client.GetAsync(SystemInfoPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, first.StatusCode);
        using var body = JsonDocument.Parse(await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("feature.disabled", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    private static async Task AssertServedAsync(HttpClient client, Uri path, int times)
    {
        for (var i = 0; i < times; i++)
        {
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static async Task<HttpStatusCode> StatusForAsync(HttpClient client, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, SystemInfoPath);
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        return response.StatusCode;
    }
}
