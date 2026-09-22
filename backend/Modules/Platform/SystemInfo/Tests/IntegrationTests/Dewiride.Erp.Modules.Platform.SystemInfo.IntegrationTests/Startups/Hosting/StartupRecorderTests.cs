using Dewiride.Erp.Modules.Platform.SystemInfo.Persistence;
using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Hosting;
using Dewiride.Erp.Testing;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.IntegrationTests.Startups.Hosting;

public sealed class StartupRecorderTests
{
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
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/healthz/live", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode);
    }
}
