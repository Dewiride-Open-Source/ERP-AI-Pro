using System.Net;
using Dewiride.Erp.BuildingBlocks.Observability.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;

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

    private static ServiceProvider Build(HttpMessageHandler handler)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.AddErpHttpClientDefaults();
        builder.Services.AddHttpClient(ClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        builder.Services.PostConfigureAll<HttpStandardResilienceOptions>(options => options.Retry.Delay = TimeSpan.Zero);

        return builder.Services.BuildServiceProvider();
    }

    private sealed class FailOnceHandler : HttpMessageHandler
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(Interlocked.Increment(ref _calls) == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
    }
}
