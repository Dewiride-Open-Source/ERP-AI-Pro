using System.Net;
using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Pipeline;

public sealed class ConfigurationRefreshTests
{
    [Fact]
    public async Task Get_AppConfigurationSource_TriggersOneRefreshPerRequestBurst()
    {
        var refresher = new CountingRefresher();
        using var factory = CreateFactory(refresher);
        using var client = factory.CreateClient();

        using var first = await client.GetAsync(new Uri("/api/platform/system-info", UriKind.Relative), TestContext.Current.CancellationToken);
        using var second = await client.GetAsync(new Uri("/api/platform/system-info", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, refresher.Calls);
    }

    [Fact]
    public async Task Get_RequestFailingLaterInThePipeline_HasAlreadyTriggeredTheRefresh()
    {
        var refresher = new CountingRefresher();
        using var factory = CreateFactory(refresher);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(ErpApiFactory.ThrowingPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, refresher.Calls);
    }

    [Fact]
    public async Task Get_InMemorySource_NeverTouchesARefresher()
    {
        var refresher = new CountingRefresher();
        using var factory = new ErpApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IConfigurationRefresherProvider>(new CountingRefresherProvider(refresher))));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/platform/system-info", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, refresher.Calls);
    }

    private static WebApplicationFactory<Program> CreateFactory(CountingRefresher refresher) =>
        new ErpApiFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton(new ErpConfigurationInfo(ErpConfigurationSource.AppConfiguration, ErpEnvironmentNames.LocalDev, refresher.AppConfigurationEndpoint));
            services.AddSingleton<IConfigurationRefresherProvider>(new CountingRefresherProvider(refresher));
        }));

    private sealed class CountingRefresherProvider(IConfigurationRefresher refresher) : IConfigurationRefresherProvider
    {
        public IEnumerable<IConfigurationRefresher> Refreshers { get; } = [refresher];
    }

    private sealed class CountingRefresher : IConfigurationRefresher
    {
        private int _calls;

        public Uri AppConfigurationEndpoint { get; } = new("https://example.azconfig.io");

        public int Calls => Volatile.Read(ref _calls);

        public Task RefreshAsync(CancellationToken cancellationToken = default) => TryRefreshAsync(cancellationToken);

        public Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(true);
        }

        public void ProcessPushNotification(PushNotification pushNotification, TimeSpan? maxDelay = null)
        {
        }
    }
}
