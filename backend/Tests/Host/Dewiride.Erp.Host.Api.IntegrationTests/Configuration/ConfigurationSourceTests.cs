using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.Testing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using HostModules = Dewiride.Erp.Host.Composition.Modules;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Configuration;

public sealed class ConfigurationSourceTests : IClassFixture<ErpApiFactory>
{
    private static readonly string[] PlatformHealthChecks =
        ["self", "database:audit", "database:files", "database:platform_caching", "database:platform_idempotency", "database:platform_jobs", "storage:attachments"];

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
    public void Services_InMemorySource_RegistersTheSelfDatabaseAndStorageHealthChecks()
    {
        var registrations = _factory.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        var moduleDatabaseChecks = HostModules.All.Select(module => module.Descriptor.Schema).OfType<string>().Select(schema => $"database:{schema}");

        Assert.Equal(
            [.. PlatformHealthChecks.Concat(moduleDatabaseChecks).Order(StringComparer.Ordinal)],
            registrations.Select(r => r.Name).Order(StringComparer.Ordinal));
    }
}
