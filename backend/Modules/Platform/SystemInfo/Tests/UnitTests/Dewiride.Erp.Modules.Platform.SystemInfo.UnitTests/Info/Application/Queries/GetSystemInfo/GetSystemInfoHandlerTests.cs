using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.Modules.Platform.SystemInfo.Info.Application.Queries.GetSystemInfo;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.UnitTests.Info.Application.Queries.GetSystemInfo;

public sealed class GetSystemInfoHandlerTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 9, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_AfterUptimeElapsed_ReportsIdentityAndUptime()
    {
        var time = new FakeTimeProvider(StartedAt);
        var handler = new GetSystemInfoHandler(new ApplicationInfo("ERP-AI-Pro Test", "1.2.3+abc", StartedAt), time);
        time.Advance(TimeSpan.FromMinutes(90));

        var result = await handler.HandleAsync(new GetSystemInfoQuery(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("ERP-AI-Pro Test", result.Value.ApplicationName);
        Assert.Equal("1.2.3+abc", result.Value.Version);
        Assert.Equal(StartedAt, result.Value.StartedAt);
        Assert.Equal(TimeSpan.FromMinutes(90), result.Value.Uptime);
    }
}
