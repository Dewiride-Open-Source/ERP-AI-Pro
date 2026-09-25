using System.Net;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Observability.Health;
using Dewiride.Erp.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Health;

public sealed class HealthEndpointsTests : IClassFixture<ErpApiFactory>
{
    private const string FailingCheck = "failing-dependency";

    private readonly HttpClient _client;

    public HealthEndpointsTests(ErpApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/healthz/live")]
    [InlineData("/healthz/ready")]
    public async Task Get_HealthEndpoint_ReturnsHealthy(string path)
    {
        using var response = await _client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Get_ReadinessWithAFailingDependency_AnswersAServiceUnavailableProblemNamingNoCheck()
    {
        using var factory = new ErpApiFactory();
        using var failing = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddHealthChecks().AddCheck(FailingCheck, () => HealthCheckResult.Unhealthy("connection refused"), tags: [HealthEndpoints.ReadyTag])));
        using var client = failing.CreateClient();

        using var response = await client.GetAsync(new Uri("/healthz/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(FailingCheck, body, StringComparison.Ordinal);
        Assert.DoesNotContain("connection refused", body, StringComparison.Ordinal);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("/problems/service.unavailable", problem!.Type);
        Assert.Equal("service.unavailable", problem.Extensions["code"]?.ToString());
        Assert.Equal(HealthEndpoints.UnhealthyTitle, problem.Title);
        Assert.Equal(Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)), problem.Extensions["traceId"]?.ToString());
    }

    [Fact]
    public async Task Get_LivenessWithAFailingDependency_StaysHealthy()
    {
        using var factory = new ErpApiFactory();
        using var failing = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddHealthChecks().AddCheck(FailingCheck, () => HealthCheckResult.Unhealthy(), tags: [HealthEndpoints.ReadyTag])));
        using var client = failing.CreateClient();

        using var response = await client.GetAsync(new Uri("/healthz/live", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
