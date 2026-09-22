using System.Net;
using System.Runtime.InteropServices;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.Modules.Platform.SystemInfo.Persistence;
using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Application.Queries.ListRecentStartups;
using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Domain;
using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Endpoints.Responses;
using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Hosting;
using Dewiride.Erp.Testing;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.IntegrationTests.Startups.Endpoints;

public sealed class StartupEndpointsTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private static readonly Uri StartupsPath = new("/api/platform/system-info/startups", UriKind.Relative);

    [Fact]
    public async Task Get_Startups_ListsTheRunningApisOwnStartWithoutTheMachineName()
    {
        using var client = factory.CreateClient();
        var recorded = await factory.Services.GetRequiredService<StartupRecorder>().Recorded.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        var application = factory.Services.GetRequiredService<ApplicationInfo>();

        using var response = await client.GetAsync(StartupsPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<RecentStartupsResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        var own = Assert.Single(body.Startups, s => s.Id == recorded.Value.Value);
        Assert.Equal(application.Name, own.ApplicationName);
        Assert.Equal(application.Version, own.Version);
        Assert.Equal(RuntimeInformation.FrameworkDescription, own.Framework);
        Assert.Equal(factory.Environment, own.EnvironmentName);
        Assert.Null(own.ConfigurationLabel);
        Assert.Equal(application.StartedAt.ToUniversalTime(), own.StartedAt);
        Assert.True(own.RecordedAt >= own.StartedAt);
        Assert.DoesNotContain(Environment.MachineName, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_Startups_WithMoreThanTwentyRows_ReturnsTheTwentyNewestFirst()
    {
        using var client = factory.CreateClient();
        await factory.Services.GetRequiredService<StartupRecorder>().Recorded.WaitAsync(TestContext.Current.CancellationToken);
        var oldest = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SystemInfoDbContext>();
            for (var i = 0; i < 25; i++)
            {
                var startedAt = oldest.AddMinutes(i);
                context.Startups.Add(ApiStartup.Record("ERP-AI-Pro (seeded)", new BuildInfo($"0.0.{i}", ".NET 10.0.0"), "Test", null, "seed", startedAt, startedAt.AddSeconds(1)).Value);
            }

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var response = await client.GetAsync(StartupsPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RecentStartupsResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(ListRecentStartupsQuery.DefaultCount, body.Startups.Count);
        Assert.Equal(body.Startups.OrderByDescending(s => s.StartedAt).ThenByDescending(s => s.RecordedAt).Select(s => s.Id), body.Startups.Select(s => s.Id));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SystemInfoDbContext>();
            var expected = await context.Startups.OrderByDescending(s => s.StartedAt).ThenByDescending(s => s.RecordedAt).Take(ListRecentStartupsQuery.DefaultCount).Select(s => s.Id.Value).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expected, body.Startups.Select(s => s.Id));
        }
    }
}
