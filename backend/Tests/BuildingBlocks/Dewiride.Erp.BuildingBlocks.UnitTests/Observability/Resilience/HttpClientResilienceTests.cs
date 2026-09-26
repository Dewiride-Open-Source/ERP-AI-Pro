using System.Net;
using Dewiride.Erp.BuildingBlocks.Observability.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Polly.CircuitBreaker;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Observability.Resilience;

public sealed class HttpClientResilienceTests
{
    private const string ClientName = "probe";

    private static readonly Uri Target = new("http://dependency.test/resource");

    [Fact]
    public async Task SendAsync_GetAfterATransientFailure_IsRetriedAndSucceeds()
    {
        var handler = new FailOnceHandler();
        using var provider = Build(handler);

        using var response = await provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName).GetAsync(Target, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task SendAsync_PostAfterATransientFailure_IsNotRetried()
    {
        var handler = new FailOnceHandler();
        using var provider = Build(handler);
        using var content = new StringContent("{}");

        using var response = await provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName).PostAsync(Target, content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task SendAsync_CircuitOpenForOneDestination_LeavesAnotherDestinationServed()
    {
        using var provider = Build(new FailingHostHandler("failing.test"), options =>
        {
            options.CircuitBreaker.MinimumThroughput = 2;
            options.CircuitBreaker.FailureRatio = 0.5;
        });
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);
        var failing = new Uri("http://failing.test/resource");
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var content = new StringContent("{}");
            using var failed = await client.PostAsync(failing, content, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        }

        await Assert.ThrowsAsync<BrokenCircuitException>(() => client.GetAsync(failing, TestContext.Current.CancellationToken));
        using var healthy = await client.GetAsync(new Uri("http://healthy.test/resource"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, healthy.StatusCode);
    }

    private static ServiceProvider Build(HttpMessageHandler handler, Action<HttpStandardResilienceOptions>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.AddErpHttpClientDefaults();
        builder.Services.AddHttpClient(ClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        builder.Services.PostConfigureAll<HttpStandardResilienceOptions>(options =>
        {
            options.Retry.Delay = TimeSpan.Zero;
            configure?.Invoke(options);
        });

        return builder.Services.BuildServiceProvider();
    }

    private sealed class FailingHostHandler(string failingHost) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(request.RequestUri?.Host == failingHost ? HttpStatusCode.InternalServerError : HttpStatusCode.OK));
    }

    private sealed class FailOnceHandler : HttpMessageHandler
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(Interlocked.Increment(ref _calls) == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
    }
}
