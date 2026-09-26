using System.Diagnostics.Metrics;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.BuildingBlocks.Observability.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Observability.Telemetry;

public sealed class OpenTelemetrySetupTests
{
    private static readonly ApplicationInfo Application = new("ERP-AI-Pro", "1.2.3", DateTimeOffset.UnixEpoch);

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("http://aspire-dashboard:18889", true)]
    public void AddErpTelemetry_OtlpEndpointSetting_RegistersTheOtlpExporterOnlyWhenSet(string? endpoint, bool registered)
    {
        var builder = CreateBuilder(new Dictionary<string, string?> { [OpenTelemetrySetup.OtlpEndpointVariable] = endpoint });

        builder.AddErpTelemetry();

        Assert.Equal(registered, builder.Services.Any(descriptor =>
            descriptor.ServiceType.Assembly == typeof(OtlpExporterOptions).Assembly
            || descriptor.ImplementationType?.Assembly == typeof(OtlpExporterOptions).Assembly));
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore.RateLimiting", true)]
    [InlineData("System.Runtime", true)]
    [InlineData("Dewiride.Erp.Finance.Sales", true)]
    [InlineData("Unlisted.Library", false)]
    public void AddErpTelemetry_Meter_IsExportedOnlyWhenListed(string meterName, bool exported)
    {
        var metrics = new List<Metric>();
        var builder = CreateBuilder([]);
        builder.AddErpTelemetry();
        builder.Services.ConfigureOpenTelemetryMeterProvider(meters => meters.AddInMemoryExporter(metrics));
        using var provider = builder.Services.BuildServiceProvider();
        var meterProvider = provider.GetRequiredService<MeterProvider>();
        var instrument = $"erp.test.{Guid.CreateVersion7():N}";

        using (var meter = new Meter(meterName))
        {
            meter.CreateCounter<long>(instrument).Add(1);
            meterProvider.ForceFlush();
        }

        Assert.Equal(exported, metrics.Any(metric => metric.Name == instrument));
    }

    [Fact]
    public void AddErpTelemetry_Resource_CarriesTheServiceNameVersionInstanceAndDeploymentEnvironment()
    {
        var builder = CreateBuilder(new Dictionary<string, string?> { [$"{ErpHostOptions.SectionName}:ApplicationName"] = "ERP-AI-Pro (local-dev)" }, label: ErpEnvironmentNames.LocalDev);
        builder.AddErpTelemetry();
        using var provider = builder.Services.BuildServiceProvider();

        var attributes = provider.GetRequiredService<TracerProvider>().GetResource().Attributes.ToDictionary(attribute => attribute.Key, attribute => attribute.Value);

        Assert.Equal("ERP-AI-Pro (local-dev)", attributes["service.name"]);
        Assert.Equal(Application.Version, attributes[ErpResourceDetector.ServiceVersionAttribute]);
        Assert.Equal(Environment.MachineName, attributes["service.instance.id"]);
        Assert.Equal(ErpEnvironmentNames.LocalDev, attributes[ErpResourceDetector.DeploymentEnvironmentAttribute]);
    }

    [Fact]
    public void AddErpTelemetry_WithoutAStoreLabel_ReportsTheHostEnvironmentAsTheDeploymentEnvironment()
    {
        var builder = CreateBuilder([], label: null);
        builder.AddErpTelemetry();
        using var provider = builder.Services.BuildServiceProvider();

        var attributes = provider.GetRequiredService<TracerProvider>().GetResource().Attributes.ToDictionary(attribute => attribute.Key, attribute => attribute.Value);

        Assert.Equal(Environments.Production, attributes[ErpResourceDetector.DeploymentEnvironmentAttribute]);
    }

    private static HostApplicationBuilder CreateBuilder(Dictionary<string, string?> settings, string? label = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true, EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddSingleton(Application);
        builder.Services.AddSingleton(new ErpConfigurationInfo(label is null ? ErpConfigurationSource.InMemory : ErpConfigurationSource.AppConfiguration, label, null));

        return builder;
    }
}
