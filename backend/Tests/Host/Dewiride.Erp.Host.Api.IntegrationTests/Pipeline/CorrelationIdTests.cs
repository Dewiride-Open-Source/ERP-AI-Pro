using System.Net;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Pipeline;

public sealed class CorrelationIdTests : IClassFixture<ErpApiFactory>
{
    private const string TraceId = "0af7651916cd43dd8448eb211c80319c";

    private readonly HttpClient _client;

    public CorrelationIdTests(ErpApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/healthz/live")]
    [InlineData("/api/platform/system-info")]
    [InlineData("/api/platform/features")]
    [InlineData("/api/platform/does-not-exist")]
    public async Task Get_AnyRoute_AnswersWithACorrelationIdHeader(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var correlationId = Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName));
        Assert.True(CorrelationId.IsWellFormed(correlationId));
    }

    [Fact]
    public async Task Get_RequestCarryingAWellFormedCorrelationId_EchoesTheCallersValue()
    {
        using var request = Request("/api/platform/system-info", correlationId: "web-01H9XZ.checkout_1");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("web-01H9XZ.checkout_1", Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)));
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Get_RequestCarryingAMalformedCorrelationId_AnswersWithTheTraceIdInstead(string supplied)
    {
        using var request = Request("/api/platform/system-info", supplied, TraceId);

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(TraceId, Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)));
    }

    [Fact]
    public async Task Get_RequestWithoutACorrelationId_AnswersWithTheTraceIdOfTheRequest()
    {
        using var request = Request("/api/platform/system-info", correlationId: null, traceId: TraceId);

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(TraceId, Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)));
    }

    [Fact]
    public async Task Get_UnknownRoute_ReportsTheCorrelationIdAsTheTraceIdOfTheProblem()
    {
        using var request = Request("/api/platform/does-not-exist", correlationId: "order-4711");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("order-4711", Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)));
        Assert.Equal("order-4711", problem!.Extensions["traceId"]?.ToString());
    }

    [Fact]
    public async Task Get_FailingRoute_LogsInsideTheCorrelationScopeOfTheRequest()
    {
        using var factory = new ErpApiFactory();
        using var scoped = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddFakeLogging()));
        using var client = scoped.CreateClient();
        using var request = Request(ErpApiFactory.ThrowingPath, correlationId: "order-4711");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var records = scoped.Services.GetRequiredService<FakeLogCollector>().GetSnapshot();
        var failure = Assert.Single(records, record => record.Category == "Dewiride.Erp.BuildingBlocks.Endpoints.Errors.GlobalExceptionHandler");
        Assert.Contains(failure.Scopes, scope => scope?.ToString() == $"{CorrelationIdScope.PropertyName}:order-4711");
    }

    private static HttpRequestMessage Request(string path, string? correlationId = null, string? traceId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        if (correlationId is not null)
        {
            request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, correlationId);
        }

        if (traceId is not null)
        {
            request.Headers.TryAddWithoutValidation("traceparent", $"00-{traceId}-b7ad6b7169203331-01");
        }

        return request;
    }
}
