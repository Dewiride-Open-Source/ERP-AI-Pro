using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.BuildingBlocks.Modules.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Modules.Features;

public sealed class FeatureManagementRegistrationTests
{
    private const string ModuleFlag = "Erp.Modules.Platform.SystemInfo";

    private const string PlatformFlag = "Erp.Platform.Identity.BearerTokens";

    [Fact]
    public void AddErpFeatureManagement_ResolvesTheCatalogProviderAsTheDefinitionProvider()
    {
        using var services = Build();

        Assert.IsType<CatalogFeatureDefinitionProvider>(services.GetRequiredService<IFeatureDefinitionProvider>());
        Assert.NotNull(services.GetRequiredService<FeatureCatalog>());
    }

    [Fact]
    public async Task IsEnabledAsync_UndefinedModuleFlag_ReturnsTrue()
    {
        using var services = Build();
        using var scope = services.CreateScope();

        var enabled = await scope.ServiceProvider.GetRequiredService<IVariantFeatureManagerSnapshot>().IsEnabledAsync(ModuleFlag, TestContext.Current.CancellationToken);

        Assert.True(enabled);
    }

    [Fact]
    public async Task IsEnabledAsync_ModuleFlagDisabledInConfiguration_ReturnsFalse()
    {
        using var services = Build(("feature_management:feature_flags:0:id", ModuleFlag), ("feature_management:feature_flags:0:enabled", "false"));
        using var scope = services.CreateScope();

        var enabled = await scope.ServiceProvider.GetRequiredService<IVariantFeatureManagerSnapshot>().IsEnabledAsync(ModuleFlag, TestContext.Current.CancellationToken);

        Assert.False(enabled);
    }

    [Fact]
    public async Task IsEnabledAsync_UndefinedPlatformFlagDisabledByDefault_ReturnsFalseAndTheCatalogListsIt()
    {
        using var services = Build(new ConfigurationBuilder().Build(), PlatformFlag);
        using var scope = services.CreateScope();

        var enabled = await scope.ServiceProvider.GetRequiredService<IVariantFeatureManagerSnapshot>().IsEnabledAsync(PlatformFlag, TestContext.Current.CancellationToken);

        Assert.False(enabled);
        Assert.True(services.GetRequiredService<FeatureCatalog>().TryGet(PlatformFlag, out var feature));
        Assert.False(feature.EnabledByDefault);
    }

    [Fact]
    public async Task IsEnabledAsync_PlatformFlagEnabledInConfiguration_ReturnsTrue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["feature_management:feature_flags:0:id"] = PlatformFlag,
                ["feature_management:feature_flags:0:enabled"] = "true",
            })
            .Build();
        using var services = Build(configuration, PlatformFlag);
        using var scope = services.CreateScope();

        var enabled = await scope.ServiceProvider.GetRequiredService<IVariantFeatureManagerSnapshot>().IsEnabledAsync(PlatformFlag, TestContext.Current.CancellationToken);

        Assert.True(enabled);
    }

    [Fact]
    public async Task IsEnabledAsync_FlagDisabledInALaterProviderAtAHigherIndex_ReturnsFalse()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["feature_management:feature_flags:1:id"] = ModuleFlag,
                ["feature_management:feature_flags:1:enabled"] = "true",
            })
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["feature_management:feature_flags:0:id"] = ModuleFlag,
                ["feature_management:feature_flags:0:enabled"] = "false",
            })
            .Build();
        using var services = Build(configuration);
        using var scope = services.CreateScope();

        var enabled = await scope.ServiceProvider.GetRequiredService<IVariantFeatureManagerSnapshot>().IsEnabledAsync(ModuleFlag, TestContext.Current.CancellationToken);

        Assert.False(enabled);
    }

    private static ServiceProvider Build(params (string Key, string? Value)[] values) =>
        Build(new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase)).Build());

    private static ServiceProvider Build(IConfiguration configuration, params string[] platformFlags)
    {
        var services = new ServiceCollection()
            .AddSingleton(configuration)
            .AddSingleton(new ModuleCatalog([new StubModule("Platform", "SystemInfo")]))
            .AddErpFeatureManagement();
        foreach (var flag in platformFlags)
        {
            services.AddErpPlatformFeature(flag, enabledByDefault: false);
        }

        return services.BuildServiceProvider();
    }
}
