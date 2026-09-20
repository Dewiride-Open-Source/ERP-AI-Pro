using System.Net;
using System.Text.Json;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Features;

public sealed class FeatureEndpointsTests
{
    private const string SystemInfoFlag = "Erp.Modules.Platform.SystemInfo";
    private static readonly Uri FeaturesPath = new("/api/platform/features", UriKind.Relative);
    private static readonly Uri SystemInfoPath = new("/api/platform/system-info", UriKind.Relative);

    [Fact]
    public async Task Get_Features_Default_ListsEveryCatalogFlagAsEnabled()
    {
        using var factory = new ErpApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(FeaturesPath, TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var feature = Assert.Single(body.RootElement.GetProperty("features").EnumerateArray());
        Assert.Equal(SystemInfoFlag, feature.GetProperty("name").GetString());
        Assert.True(feature.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Get_Features_WithTheModuleDisabled_ReportsItDisabledAndStaysReachable()
    {
        using var factory = new ErpApiFactory().WithFeature(SystemInfoFlag, enabled: false);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(FeaturesPath, TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var feature = Assert.Single(body.RootElement.GetProperty("features").EnumerateArray());
        Assert.False(feature.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Get_ModuleRoute_WithTheModuleDisabled_ReturnsNotFoundProblemDetails()
    {
        using var factory = new ErpApiFactory().WithFeature(SystemInfoFlag, enabled: false);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(SystemInfoPath, TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("feature.disabled", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("/api/platform/system-info", body.RootElement.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Get_ModuleRoute_WithTheModuleReenabledLater_Serves()
    {
        using var factory = new ErpApiFactory().WithFeature(SystemInfoFlag, enabled: false).WithFeature(SystemInfoFlag, enabled: true);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(SystemInfoPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
