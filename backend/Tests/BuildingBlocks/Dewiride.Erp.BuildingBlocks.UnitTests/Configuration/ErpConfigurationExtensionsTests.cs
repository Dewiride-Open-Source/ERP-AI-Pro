using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.Configuration;
using Dewiride.Erp.BuildingBlocks.Configuration.AppConfiguration;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.AppConfiguration.Fakes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Configuration;

public sealed class ErpConfigurationExtensionsTests
{
    private const string ApplicationNameKey = "Erp:Platform:Host:ApplicationName";

    private static readonly Uri Endpoint = new("https://example.azconfig.io");

    [Fact]
    public void AddErpConfiguration_EndpointSet_LoadsTheStoreAndRegistersTheRefresherProvider()
    {
        var client = new FakeConfigurationClient()
            .With(ApplicationNameKey, "ERP-AI-Pro")
            .With(ApplicationNameKey, "ERP-AI-Pro (local-dev)", ErpEnvironmentNames.LocalDev);
        var builder = CreateBuilder(
            Environments.Development,
            (ErpConfigurationSourceResolver.EndpointVariable, Endpoint.AbsoluteUri),
            (ErpEnvironmentNames.VariableName, ErpEnvironmentNames.LocalDev));

        builder.AddErpConfiguration(
            typeof(ErpConfigurationExtensionsTests).Assembly,
            name => name == AzureCredentialFactory.SelectionVariable ? AzureCredentialFactory.DevelopmentSelection : null,
            options => options.SetClientFactory(new FakeConfigurationClientFactory(client)));

        Assert.Equal(new ErpConfigurationInfo(ErpConfigurationSource.AppConfiguration, ErpEnvironmentNames.LocalDev, Endpoint), RegisteredInfo(builder));
        Assert.Equal("ERP-AI-Pro (local-dev)", builder.Configuration[ApplicationNameKey]);
        Assert.Equal("true", Environment.GetEnvironmentVariable(AppConfigurationSetup.FeatureFlagSchemaVariable));
        using var provider = builder.Services.BuildServiceProvider();
        var refresher = Assert.Single(provider.GetRequiredService<IConfigurationRefresherProvider>().Refreshers);
        Assert.Equal(Endpoint, refresher.AppConfigurationEndpoint);
    }

    [Fact]
    public void AddErpConfiguration_EndpointSetWithoutCredentialSelection_ThrowsNamingAzureTokenCredentials()
    {
        var builder = CreateBuilder(
            Environments.Development,
            (ErpConfigurationSourceResolver.EndpointVariable, Endpoint.AbsoluteUri),
            (ErpEnvironmentNames.VariableName, ErpEnvironmentNames.LocalDev));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddErpConfiguration(typeof(ErpConfigurationExtensionsTests).Assembly, _ => null, configureProvider: null));

        Assert.Contains(AzureCredentialFactory.SelectionVariable, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IConfigurationRefresherProvider));
    }

    [Fact]
    public void AddErpConfiguration_ProductionWithoutEndpoint_ThrowsNamingAppConfigEndpoint()
    {
        var builder = CreateBuilder(Environments.Production);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddErpConfiguration(typeof(ErpConfigurationExtensionsTests).Assembly));

        Assert.Contains(ErpConfigurationSourceResolver.EndpointVariable, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddErpConfiguration_DevelopmentWithoutEndpoint_UsesLocalDevelopmentSource()
    {
        var builder = CreateBuilder(Environments.Development);

        builder.AddErpConfiguration(typeof(ErpConfigurationExtensionsTests).Assembly);

        Assert.Equal(ErpConfigurationSource.LocalDevelopment, builder.GetErpConfigurationInfo().Source);
        Assert.Equal(ErpConfigurationSource.LocalDevelopment, RegisteredInfo(builder).Source);
    }

    [Fact]
    public void AddErpConfiguration_InMemorySetting_UsesInMemorySource()
    {
        var builder = CreateBuilder(
            Environments.Production,
            (ErpConfigurationSourceResolver.SourceSetting, ErpConfigurationSourceResolver.InMemorySource));

        builder.AddErpConfiguration(typeof(ErpConfigurationExtensionsTests).Assembly);

        Assert.Equal(new ErpConfigurationInfo(ErpConfigurationSource.InMemory, null, null), builder.GetErpConfigurationInfo());
        Assert.Equal(new ErpConfigurationInfo(ErpConfigurationSource.InMemory, null, null), RegisteredInfo(builder));
    }

    [Fact]
    public void AddErpConfiguration_InMemorySource_DoesNotRegisterRefresherProvider()
    {
        var builder = CreateBuilder(
            Environments.Development,
            (ErpConfigurationSourceResolver.SourceSetting, ErpConfigurationSourceResolver.InMemorySource));

        builder.AddErpConfiguration(typeof(ErpConfigurationExtensionsTests).Assembly);

        Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IConfigurationRefresherProvider));
        Assert.Equal(ErpConfigurationSource.InMemory, RegisteredInfo(builder).Source);
    }

    [Fact]
    public void AddErpConfiguration_RegistersValidatedRefreshOptions()
    {
        var builder = CreateBuilder(
            Environments.Development,
            (ErpConfigurationSourceResolver.SourceSetting, ErpConfigurationSourceResolver.InMemorySource));

        builder.AddErpConfiguration(typeof(ErpConfigurationExtensionsTests).Assembly);

        var descriptor = Assert.Single(builder.Services, d => d.ServiceType == typeof(AppConfigurationRefreshOptions));
        var options = Assert.IsType<AppConfigurationRefreshOptions>(descriptor.ImplementationInstance);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
        Assert.Equal(TimeSpan.FromMinutes(30), options.RefreshInterval);
        Assert.Equal(TimeSpan.FromHours(1), options.SecretRefreshInterval);
        Assert.Equal(TimeSpan.FromMinutes(1), options.StartupTimeout);
    }

    [Fact]
    public void AddErpConfiguration_RefreshIntervalFromBootstrap_RegistersTheBoundValue()
    {
        var builder = CreateBuilder(
            Environments.Development,
            (ErpConfigurationSourceResolver.SourceSetting, ErpConfigurationSourceResolver.InMemorySource),
            ($"{AppConfigurationRefreshOptions.SectionName}:RefreshInterval", "00:05:00"));

        builder.AddErpConfiguration(typeof(ErpConfigurationExtensionsTests).Assembly);

        var descriptor = Assert.Single(builder.Services, d => d.ServiceType == typeof(AppConfigurationRefreshOptions));
        var options = Assert.IsType<AppConfigurationRefreshOptions>(descriptor.ImplementationInstance);
        Assert.Equal(TimeSpan.FromMinutes(5), options.RefreshInterval);
    }

    [Fact]
    public void AddErpConfiguration_RefreshIntervalBelowOneSecond_ThrowsNamingTheSection()
    {
        var builder = CreateBuilder(
            Environments.Development,
            (ErpConfigurationSourceResolver.SourceSetting, ErpConfigurationSourceResolver.InMemorySource),
            ($"{AppConfigurationRefreshOptions.SectionName}:RefreshInterval", "00:00:00"));

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddErpConfiguration(typeof(ErpConfigurationExtensionsTests).Assembly));

        Assert.Contains(AppConfigurationRefreshOptions.SectionName, exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(AppConfigurationRefreshOptions.RefreshInterval), exception.Message, StringComparison.Ordinal);
        Assert.IsType<ValidationException>(exception.InnerException);
    }

    [Fact]
    public async Task UseErpConfigurationRefresh_InMemorySource_DoesNotAddTheMiddleware()
    {
        var services = new ServiceCollection()
            .AddSingleton(new ErpConfigurationInfo(ErpConfigurationSource.InMemory, null, null))
            .BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        var reachedTerminal = false;

        app.UseErpConfigurationRefresh();
        app.Run(_ => { reachedTerminal = true; return Task.CompletedTask; });
        await app.Build()(new DefaultHttpContext());

        Assert.True(reachedTerminal);
    }

    [Fact]
    public async Task UseErpConfigurationRefresh_AppConfigurationSource_AddsTheMiddleware()
    {
        var refresher = new FakeConfigurationRefresher();
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(new ErpConfigurationInfo(ErpConfigurationSource.AppConfiguration, ErpEnvironmentNames.LocalDev, Endpoint))
            .AddSingleton<IConfigurationRefresherProvider>(new FakeConfigurationRefresherProvider(refresher))
            .AddSingleton<TimeProvider>(new FakeTimeProvider())
            .BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        var reachedTerminal = false;

        app.UseErpConfigurationRefresh();
        app.Run(_ => { reachedTerminal = true; return Task.CompletedTask; });
        await app.Build()(new DefaultHttpContext());

        Assert.True(reachedTerminal);
        Assert.Equal(1, refresher.Calls);
    }

    [Theory]
    [InlineData("not-a-network")]
    [InlineData("172.28.0.5/16")]
    public async Task StartAsync_KnownNetworkThatIsNotANetwork_FailsNamingTheEntry(string network)
    {
        const string entry = $"{ErpHostOptions.SectionName}:KnownNetworks:0";
        var builder = CreateBuilder(
            Environments.Production,
            (ErpConfigurationSourceResolver.SourceSetting, ErpConfigurationSourceResolver.InMemorySource),
            (entry, network));
        builder.AddErpConfiguration(typeof(ErpConfigurationExtensionsTests).Assembly);
        using var host = builder.Build();

        var failure = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains(entry, failure.Message, StringComparison.Ordinal);
        Assert.Contains(network, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetErpConfigurationInfo_BeforeAddErpConfiguration_Throws()
    {
        var builder = CreateBuilder(Environments.Development);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.GetErpConfigurationInfo());

        Assert.Contains(nameof(ErpConfigurationExtensions.AddErpConfiguration), exception.Message, StringComparison.Ordinal);
    }

    private static HostApplicationBuilder CreateBuilder(string environmentName, params (string Key, string? Value)[] values)
    {
        var bootstrap = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [ErpConfigurationSourceResolver.EndpointVariable] = string.Empty,
        };
        foreach (var (key, value) in values)
        {
            bootstrap[key] = value;
        }

        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(bootstrap);

        return Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            EnvironmentName = environmentName,
            Configuration = configuration,
        });
    }

    private static ErpConfigurationInfo RegisteredInfo(HostApplicationBuilder builder)
    {
        var descriptor = Assert.Single(builder.Services, d => d.ServiceType == typeof(ErpConfigurationInfo));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);

        return Assert.IsType<ErpConfigurationInfo>(descriptor.ImplementationInstance);
    }
}
