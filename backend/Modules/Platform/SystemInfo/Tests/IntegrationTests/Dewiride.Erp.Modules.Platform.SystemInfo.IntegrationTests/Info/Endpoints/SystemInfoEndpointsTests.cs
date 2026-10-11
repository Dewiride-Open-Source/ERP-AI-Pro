using System.Net;
using System.Reflection;
using System.Text.Json;
using Dewiride.Erp.Modules.Platform.SystemInfo.Contracts;
using Dewiride.Erp.Modules.Platform.SystemInfo.Info.Endpoints.Responses;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authorization;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.IntegrationTests.Info.Endpoints;

public sealed class SystemInfoEndpointsTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;

    private readonly HttpClient _client;

    public SystemInfoEndpointsTests(ErpApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient().AsUser(TestUsers.Administrator);
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

    [Fact]
    public async Task Get_SystemInfoAsAnEmployee_Answers403RequestForbidden()
    {
        using var client = _factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await client.GetAsync(new Uri("/api/platform/system-info", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("request.forbidden", (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("GET", "/api/platform/system-info", SystemInfoPermissions.ReadInfo)]
    public void Route_OfEachUseCase_RequiresItsOwnPermission(string method, string route, string permission)
    {
        Assert.Equal(permission, RoutePermissions.Of(_factory.Services, method, route));
    }

    private static string HostVersion() =>
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
}
