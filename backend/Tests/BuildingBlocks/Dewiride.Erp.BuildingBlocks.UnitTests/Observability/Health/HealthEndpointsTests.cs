using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.BuildingBlocks.Observability.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Observability.Health;

public sealed class HealthEndpointsTests
{
    [Fact]
    public void AddErpHealthChecks_AppConfigurationSource_RegistersTheStoreCheckAsReadyThatOnlyDegrades()
    {
        var builder = CreateBuilder(new ErpConfigurationInfo(ErpConfigurationSource.AppConfiguration, ErpEnvironmentNames.LocalDev, new Uri("https://example.azconfig.io")));

        builder.AddErpHealthChecks();

        var registrations = Registrations(builder);
        Assert.Equal(["self", "app-configuration"], registrations.Select(r => r.Name));
        Assert.All(registrations, r => Assert.Contains(HealthEndpoints.ReadyTag, r.Tags));
        Assert.Equal(HealthStatus.Degraded, registrations.Single(r => r.Name == "app-configuration").FailureStatus);
    }

    [Fact]
    public void AddErpHealthChecks_ChecksWithoutATimeout_GetTheCheckTimeoutWhileAnExplicitTimeoutIsKept()
    {
        var builder = CreateBuilder(new ErpConfigurationInfo(ErpConfigurationSource.InMemory, null, null));
        builder.Services.AddHealthChecks()
            .AddCheck("registered-earlier", () => HealthCheckResult.Healthy(), tags: [HealthEndpoints.ReadyTag])
            .AddCheck("explicit", () => HealthCheckResult.Healthy(), tags: [HealthEndpoints.ReadyTag], timeout: TimeSpan.FromSeconds(1));

        builder.AddErpHealthChecks();
        builder.Services.AddHealthChecks().AddCheck("registered-later", () => HealthCheckResult.Healthy(), tags: [HealthEndpoints.ReadyTag]);

        var timeouts = Registrations(builder).ToDictionary(r => r.Name, r => r.Timeout);
        Assert.Equal(HealthEndpoints.CheckTimeout, timeouts["self"]);
        Assert.Equal(HealthEndpoints.CheckTimeout, timeouts["registered-earlier"]);
        Assert.Equal(HealthEndpoints.CheckTimeout, timeouts["registered-later"]);
        Assert.Equal(TimeSpan.FromSeconds(1), timeouts["explicit"]);
    }

    [Theory]
    [InlineData(ErpConfigurationSource.InMemory)]
    [InlineData(ErpConfigurationSource.LocalDevelopment)]
    public void AddErpHealthChecks_WithoutTheStore_RegistersOnlyTheSelfCheck(ErpConfigurationSource source)
    {
        var builder = CreateBuilder(new ErpConfigurationInfo(source, null, null));

        builder.AddErpHealthChecks();

        Assert.Equal(["self"], Registrations(builder).Select(r => r.Name));
    }

    [Fact]
    public void AddErpHealthChecks_BeforeAddErpConfiguration_Throws()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        Assert.Throws<InvalidOperationException>(() => builder.AddErpHealthChecks());
    }

    private static HostApplicationBuilder CreateBuilder(ErpConfigurationInfo info)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        ((IHostApplicationBuilder)builder).Properties[typeof(ErpConfigurationInfo)] = info;

        return builder;
    }

    private static IReadOnlyList<HealthCheckRegistration> Registrations(HostApplicationBuilder builder)
    {
        using var provider = builder.Services.BuildServiceProvider();

        return [.. provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations];
    }
}
