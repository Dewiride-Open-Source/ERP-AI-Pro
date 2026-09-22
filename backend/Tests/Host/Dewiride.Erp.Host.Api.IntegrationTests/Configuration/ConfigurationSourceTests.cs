using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.Testing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Configuration;

public sealed class ConfigurationSourceTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;

    public ConfigurationSourceTests(ErpApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Services_DevelopmentFactory_ResolvesInMemorySource()
    {
        var info = _factory.Services.GetRequiredService<ErpConfigurationInfo>();

        Assert.Equal(new ErpConfigurationInfo(ErpConfigurationSource.InMemory, null, null), info);
    }

    [Fact]
    public void Services_ProductionFactory_ResolvesInMemorySource()
    {
        using var factory = ErpApiFactory.ForEnvironment(Environments.Production);

        var info = factory.Services.GetRequiredService<ErpConfigurationInfo>();

        Assert.Equal(new ErpConfigurationInfo(ErpConfigurationSource.InMemory, null, null), info);
    }

    [Fact]
    public void Services_InMemorySource_RegistersTheSelfAndModuleDatabaseHealthChecks()
    {
        var registrations = _factory.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        Assert.Equal(["database:platform_idempotency", "database:platform_system_info", "self"], registrations.Select(r => r.Name).Order(StringComparer.Ordinal));
    }
}
