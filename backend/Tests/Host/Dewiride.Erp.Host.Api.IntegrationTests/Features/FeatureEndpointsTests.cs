using System.Net;
using System.Text.Json;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Features;

public sealed class FeatureEndpointsTests
{
    private const string SystemInfoFlag = "Erp.Modules.Platform.SystemInfo";

    private const string AttachmentsFlag = "Erp.Modules.Platform.Attachments";

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
        var features = body.RootElement.GetProperty("features").EnumerateArray().ToList();
        Assert.Equal([AttachmentsFlag, SystemInfoFlag], features.Select(f => f.GetProperty("name").GetString()).Order(StringComparer.Ordinal));
        Assert.All(features, f => Assert.True(f.GetProperty("enabled").GetBoolean()));
    }

    [Fact]
    public async Task Get_Features_WithTheModuleDisabled_ReportsItDisabledAndStaysReachable()
    {
        using var factory = new ErpApiFactory().WithFeature(SystemInfoFlag, enabled: false);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(FeaturesPath, TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var features = body.RootElement.GetProperty("features").EnumerateArray().ToDictionary(f => f.GetProperty("name").GetString()!, f => f.GetProperty("enabled").GetBoolean(), StringComparer.Ordinal);
        Assert.False(features[SystemInfoFlag]);
        Assert.True(features[AttachmentsFlag]);
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

    [Theory]
    [InlineData("text/html")]
    [InlineData("application/xml")]
    public async Task Get_ModuleRoute_WithTheModuleDisabledForAClientThatAcceptsNoJson_ReturnsNotFoundAsPlainText(string accept)
    {
        using var factory = new ErpApiFactory().WithFeature(SystemInfoFlag, enabled: false);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, SystemInfoPath);
        request.Headers.Accept.ParseAdd(accept);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
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
