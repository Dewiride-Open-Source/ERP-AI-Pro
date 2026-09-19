using System.Net;
using System.Reflection;
using Dewiride.Erp.Modules.Platform.SystemInfo.Info.Endpoints.Responses;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.IntegrationTests.Info.Endpoints;

public sealed class SystemInfoEndpointsTests : IClassFixture<ErpApiFactory>
{
    private readonly HttpClient _client;

    public SystemInfoEndpointsTests(ErpApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_SystemInfo_ReportsTheHostAssemblyVersionAndStartTime()
    {
        using var response = await _client.GetAsync(new Uri("/api/platform/system-info", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SystemInfoResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal("ERP-AI-Pro", body.ApplicationName);
        Assert.Equal(HostVersion(), body.Version);
        Assert.True(body.StartedAt <= TimeProvider.System.GetUtcNow());
        Assert.True(body.UptimeSeconds >= 0);
    }

    private static string HostVersion() =>
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
}
