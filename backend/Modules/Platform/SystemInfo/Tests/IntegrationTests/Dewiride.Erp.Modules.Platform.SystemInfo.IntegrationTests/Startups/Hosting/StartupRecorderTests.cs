using System.Net;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.Modules.Platform.SystemInfo.Persistence;
using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Domain;
using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Hosting;
using Dewiride.Erp.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.IntegrationTests.Startups.Hosting;

public sealed class StartupRecorderTests
{
    private static readonly Uri LivePath = new("/healthz/live", UriKind.Relative);

    [Fact]
    public async Task ExecuteAsync_ModuleEnabled_WritesOneRowForThisProcess()
    {
        using var factory = new ErpApiFactory();

        var recorded = await factory.Services.GetRequiredService<StartupRecorder>().Recorded.WaitAsync(TestContext.Current.CancellationToken);

        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SystemInfoDbContext>();
        var row = await context.Startups.SingleAsync(s => s.Id == recorded.Value, TestContext.Current.CancellationToken);
        Assert.Equal(Environment.MachineName, row.MachineName);
    }

    [Fact]
    public async Task ExecuteAsync_ModuleDisabled_SkipsRecordingAndKeepsTheHostRunning()
    {
        using var factory = new ErpApiFactory().WithFeature(SystemInfoModule.FeatureFlag, enabled: false);

        var recorded = await factory.Services.GetRequiredService<StartupRecorder>().Recorded.WaitAsync(TestContext.Current.CancellationToken);

        Assert.True(recorded.IsFailure);
        Assert.Equal("startup.module-disabled", recorded.Error!.Code);
        await AssertLiveAsync(factory);
    }

    [Fact]
    public async Task ExecuteAsync_ClockBeforeTheProcessStart_ReportsTheAggregateRuleAndKeepsTheHostRunning()
    {
        using var root = new ErpApiFactory();
        using var factory = root.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)))));

        var recorded = await factory.Services.GetRequiredService<StartupRecorder>().Recorded.WaitAsync(TestContext.Current.CancellationToken);

        Assert.True(recorded.IsFailure);
        Assert.Equal(ApiStartupErrors.RecordedBeforeStart, recorded.Error);
        await AssertLiveAsync(factory);
    }

    [Fact]
    public async Task ExecuteAsync_DatabaseUnreachable_ReportsTheFailureAndKeepsTheHostRunning()
    {
        using var factory = new ErpApiFactory()
            .WithConfiguration(ErpApiFactory.DatabaseConnectionKey, "Server=127.0.0.1,1;Database=ErpAiProTest_unreachable;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=1;ConnectRetryCount=0")
            .WithConfiguration($"{DatabaseOptions.SectionName}:MaxRetryCount", "0");

        var recorded = await factory.Services.GetRequiredService<StartupRecorder>().Recorded.WaitAsync(TestContext.Current.CancellationToken);

        Assert.True(recorded.IsFailure);
        Assert.Equal("startup.record-failed", recorded.Error!.Code);
        await AssertLiveAsync(factory);
    }

    private static async Task AssertLiveAsync(WebApplicationFactory<Program> factory)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(LivePath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
