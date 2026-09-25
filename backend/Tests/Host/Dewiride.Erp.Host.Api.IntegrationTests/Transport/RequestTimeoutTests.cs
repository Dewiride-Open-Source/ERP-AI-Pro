using System.Net;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.Testing;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Transport;

public sealed class RequestTimeoutTests : IClassFixture<RequestTimeoutTests.Fixture>
{
    private const string SlowPath = "/__test/slow";

    private const string ExemptPath = "/__test/slow-exempt";

    private readonly HttpClient _client;

    public RequestTimeoutTests(Fixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task Get_RequestRunningPastTheTimeout_AnswersARequestTimeoutProblemFromTheOuterHandlers()
    {
        using var response = await _client.GetAsync(new Uri(SlowPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("/problems/request.timeout", problem!.Type);
        Assert.Equal(ProblemTypes.RequestTimeout, problem.Extensions["code"]?.ToString());
        Assert.Equal(Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)), problem.Extensions[ProblemTypes.TraceIdExtension]?.ToString());
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task Get_EndpointThatDisablesTheTimeout_RunsPastIt()
    {
        using var response = await _client.GetAsync(new Uri(ExemptPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Factory = new ErpApiFactory()
                .WithConfiguration($"{ErpHostOptions.SectionName}:RequestTimeout", "00:00:01")
                .WithTestEndpoints(routes =>
                {
                    routes.MapGet(SlowPath, async (CancellationToken cancellationToken) =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                        return Results.Ok();
                    });
                    routes.MapGet(ExemptPath, async (CancellationToken cancellationToken) =>
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(1500), cancellationToken);
                        return Results.Ok();
                    }).DisableRequestTimeout();
                });
        }

        public ErpApiFactory Factory { get; }

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
