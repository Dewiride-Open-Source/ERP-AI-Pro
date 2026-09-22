using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.Sources;

public sealed class ErpConfigurationSourceResolverTests
{
    private const string Endpoint = "https://example.azconfig.io";

    [Fact]
    public void Resolve_EndpointSetWithLocalDevLabel_ReturnsAppConfiguration()
    {
        var bootstrap = Bootstrap(
            (ErpConfigurationSourceResolver.EndpointVariable, Endpoint),
            (ErpEnvironmentNames.VariableName, ErpEnvironmentNames.LocalDev));

        var info = ErpConfigurationSourceResolver.Resolve(bootstrap, new FakeHostEnvironment(Environments.Development));

        Assert.Equal(ErpConfigurationSource.AppConfiguration, info.Source);
        Assert.Equal(ErpEnvironmentNames.LocalDev, info.Label);
        Assert.Equal(new Uri(Endpoint), info.Endpoint);
    }

    [Fact]
    public void Resolve_EndpointSetWithUnknownLabel_ThrowsNamingErpEnvironment()
    {
        var bootstrap = Bootstrap(
            (ErpConfigurationSourceResolver.EndpointVariable, Endpoint),
            (ErpEnvironmentNames.VariableName, "staging"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ErpConfigurationSourceResolver.Resolve(bootstrap, new FakeHostEnvironment(Environments.Production)));

        Assert.Contains(ErpEnvironmentNames.VariableName, exception.Message, StringComparison.Ordinal);
        Assert.Contains("staging", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_EndpointNotAnHttpsUri_ThrowsNamingAppConfigEndpoint()
    {
        var bootstrap = Bootstrap(
            (ErpConfigurationSourceResolver.EndpointVariable, "http://example.azconfig.io"),
            (ErpEnvironmentNames.VariableName, ErpEnvironmentNames.LocalDev));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ErpConfigurationSourceResolver.Resolve(bootstrap, new FakeHostEnvironment(Environments.Development)));

        Assert.Contains(ErpConfigurationSourceResolver.EndpointVariable, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("http://example.azconfig.io", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_InMemorySettingInDevelopment_ReturnsInMemory()
    {
        var bootstrap = Bootstrap((ErpConfigurationSourceResolver.SourceSetting, ErpConfigurationSourceResolver.InMemorySource));

        var info = ErpConfigurationSourceResolver.Resolve(bootstrap, new FakeHostEnvironment(Environments.Development));

        Assert.Equal(new ErpConfigurationInfo(ErpConfigurationSource.InMemory, null, null), info);
    }

    [Fact]
    public void Resolve_InMemorySettingInProduction_ReturnsInMemory()
    {
        var bootstrap = Bootstrap(
            (ErpConfigurationSourceResolver.EndpointVariable, string.Empty),
            (ErpConfigurationSourceResolver.SourceSetting, "inmemory"));

        var info = ErpConfigurationSourceResolver.Resolve(bootstrap, new FakeHostEnvironment(Environments.Production));

        Assert.Equal(new ErpConfigurationInfo(ErpConfigurationSource.InMemory, null, null), info);
    }

    [Fact]
    public void Resolve_EndpointEmptyInDevelopment_ReturnsLocalDevelopment()
    {
        var bootstrap = Bootstrap((ErpConfigurationSourceResolver.EndpointVariable, string.Empty));

        var info = ErpConfigurationSourceResolver.Resolve(bootstrap, new FakeHostEnvironment(Environments.Development));

        Assert.Equal(new ErpConfigurationInfo(ErpConfigurationSource.LocalDevelopment, null, null), info);
    }

    [Fact]
    public void Resolve_EndpointEmptyInProduction_ThrowsNamingAppConfigEndpoint()
    {
        var bootstrap = Bootstrap((ErpConfigurationSourceResolver.EndpointVariable, string.Empty));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ErpConfigurationSourceResolver.Resolve(bootstrap, new FakeHostEnvironment(Environments.Production)));

        Assert.Contains(ErpConfigurationSourceResolver.EndpointVariable, exception.Message, StringComparison.Ordinal);
        Assert.Contains(Environments.Production, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_LocalDevelopmentSettingWithEndpointInProduction_ReturnsLocalDevelopment()
    {
        var bootstrap = Bootstrap(
            (ErpConfigurationSourceResolver.SourceSetting, ErpConfigurationSourceResolver.LocalDevelopmentSource),
            (ErpConfigurationSourceResolver.EndpointVariable, Endpoint),
            (ErpEnvironmentNames.VariableName, ErpEnvironmentNames.Production));

        var info = ErpConfigurationSourceResolver.Resolve(bootstrap, new FakeHostEnvironment(Environments.Production));

        Assert.Equal(new ErpConfigurationInfo(ErpConfigurationSource.LocalDevelopment, null, null), info);
    }

    private static IConfiguration Bootstrap(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();
}
