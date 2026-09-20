using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Configuration.AppConfiguration;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.Testing;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Configuration;

public sealed class InMemoryConfigurationTests
{
    private const string ApplicationNameKey = $"{ErpHostOptions.SectionName}:ApplicationName";
    private const string AllowedHostsKey = $"{ErpHostOptions.SectionName}:AllowedHosts";

    [Fact]
    public async Task Get_SystemInfo_WithInMemoryApplicationName_ReportsTheOverride()
    {
        using var factory = new ErpApiFactory().WithConfiguration(ApplicationNameKey, "ERP-AI-Pro (in-memory)");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/platform/system-info", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("ERP-AI-Pro (in-memory)", body.RootElement.GetProperty("applicationName").GetString());
    }

    [Fact]
    public void Services_WithInMemoryHostOptions_BindTheOverrideOverTheAppSettingsSiblings()
    {
        using var factory = new ErpApiFactory().WithConfiguration(ApplicationNameKey, "ERP-AI-Pro (bound)");

        var options = factory.Services.GetRequiredService<IOptions<ErpHostOptions>>().Value;
        var configuration = factory.Services.GetRequiredService<IConfiguration>();

        Assert.Equal("ERP-AI-Pro (bound)", options.ApplicationName);
        Assert.Equal("*", configuration[AllowedHostsKey]);
        Assert.Equal("*", options.AllowedHosts);
    }

    [Fact]
    public void Services_WithoutOverride_ReadTheAppSettingsValue()
    {
        using var factory = new ErpApiFactory();

        var configuration = factory.Services.GetRequiredService<IConfiguration>();

        Assert.Equal("ERP-AI-Pro", configuration[ApplicationNameKey]);
        Assert.Equal("ERP-AI-Pro", factory.Services.GetRequiredService<IOptions<ErpHostOptions>>().Value.ApplicationName);
    }

    [Fact]
    public void Services_WithInMemoryBootstrapInterval_RegisterTheOverriddenValue()
    {
        using var factory = new ErpApiFactory().WithConfiguration($"{AppConfigurationRefreshOptions.SectionName}:RefreshInterval", "00:00:01");

        var refresh = factory.Services.GetRequiredService<AppConfigurationRefreshOptions>();

        Assert.Equal(TimeSpan.FromSeconds(1), refresh.RefreshInterval);
    }

    [Fact]
    public void WithConfiguration_AfterTheHostIsCreated_Throws()
    {
        using var factory = new ErpApiFactory();
        using var client = factory.CreateClient();

        var exception = Assert.Throws<InvalidOperationException>(() => factory.WithConfiguration(ApplicationNameKey, "too late"));

        Assert.Contains(nameof(ErpApiFactory.WithConfiguration), exception.Message, StringComparison.Ordinal);
    }
}
