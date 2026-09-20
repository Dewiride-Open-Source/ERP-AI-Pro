using Dewiride.Erp.BuildingBlocks.Configuration.AppConfiguration;
using Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.AppConfiguration.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.AppConfiguration;

public sealed class ConfigurationRefreshMiddlewareTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InvokeAsync_FirstRequest_TriggersRefreshAndCallsNext()
    {
        var refresher = new FakeConfigurationRefresher();
        var nextCalls = 0;
        var middleware = Create(refresher, new FakeTimeProvider(Start), _ => { nextCalls++; return Task.CompletedTask; });

        await middleware.InvokeAsync(new DefaultHttpContext());

        Assert.Equal(1, nextCalls);
        Assert.Equal(1, refresher.Calls);
    }

    [Fact]
    public async Task InvokeAsync_SecondRequestWithinOneSecond_DoesNotTriggerRefresh()
    {
        var refresher = new FakeConfigurationRefresher();
        var time = new FakeTimeProvider(Start);
        var nextCalls = 0;
        var middleware = Create(refresher, time, _ => { nextCalls++; return Task.CompletedTask; });

        await middleware.InvokeAsync(new DefaultHttpContext());
        time.Advance(TimeSpan.FromMilliseconds(999));
        await middleware.InvokeAsync(new DefaultHttpContext());

        Assert.Equal(2, nextCalls);
        Assert.Equal(1, refresher.Calls);
    }

    [Fact]
    public async Task InvokeAsync_AfterOneSecondElapsed_TriggersRefreshAgain()
    {
        var refresher = new FakeConfigurationRefresher();
        var time = new FakeTimeProvider(Start);
        var middleware = Create(refresher, time, _ => Task.CompletedTask);

        await middleware.InvokeAsync(new DefaultHttpContext());
        time.Advance(TimeSpan.FromSeconds(1));
        await middleware.InvokeAsync(new DefaultHttpContext());

        Assert.Equal(2, refresher.Calls);
    }

    [Fact]
    public async Task InvokeAsync_ConcurrentFirstRequests_TriggerOneRefresh()
    {
        var refresher = new FakeConfigurationRefresher();
        var middleware = Create(refresher, new FakeTimeProvider(Start), _ => Task.CompletedTask);

        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => middleware.InvokeAsync(new DefaultHttpContext()))));

        Assert.Equal(1, refresher.Calls);
    }

    [Fact]
    public async Task InvokeAsync_RefresherFaults_LogsTheErrorAndCallsNext()
    {
        var fault = new InvalidOperationException("certificate rejected");
        var refresher = new FakeConfigurationRefresher { Fault = fault };
        var logger = new FakeLogger<ConfigurationRefreshMiddleware>();
        var nextCalls = 0;
        var middleware = new ConfigurationRefreshMiddleware(
            _ => { nextCalls++; return Task.CompletedTask; },
            new FakeConfigurationRefresherProvider(refresher),
            new FakeTimeProvider(Start),
            logger);

        await middleware.InvokeAsync(new DefaultHttpContext());

        Assert.Equal(1, nextCalls);
        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Same(fault, record.Exception);
        Assert.Contains(refresher.AppConfigurationEndpoint.ToString(), record.Message, StringComparison.Ordinal);
    }

    private static ConfigurationRefreshMiddleware Create(FakeConfigurationRefresher refresher, TimeProvider time, RequestDelegate next) =>
        new(next, new FakeConfigurationRefresherProvider(refresher), time, NullLogger<ConfigurationRefreshMiddleware>.Instance);
}
