using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.BuildingBlocks.Modules.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Modules.Features;

public sealed class CatalogFeatureDefinitionProviderTests
{
    private const string ModuleFlag = "Erp.Modules.Finance.Sales";
    private const string CapabilityFlag = "Erp.Modules.Finance.Sales.AiDrafts";

    [Fact]
    public async Task GetFeatureDefinitionAsync_FlagDefinedInConfiguration_ReturnsTheConfiguredDefinition()
    {
        using var provider = Create(Flags((ModuleFlag, false)));

        var definition = await provider.GetFeatureDefinitionAsync(ModuleFlag);

        Assert.Equal(ModuleFlag, definition.Name);
        Assert.Equal(FeatureStatus.Disabled, definition.Status);
    }

    [Fact]
    public async Task GetFeatureDefinitionAsync_UndefinedModuleFlag_ReturnsAnAlwaysOnDefault()
    {
        using var provider = Create();

        var definition = await provider.GetFeatureDefinitionAsync(ModuleFlag);

        Assert.Equal(FeatureStatus.Conditional, definition.Status);
        Assert.Equal("AlwaysOn", Assert.Single(definition.EnabledFor).Name);
    }

    [Fact]
    public async Task GetFeatureDefinitionAsync_UndefinedCapabilityFlagDisabledByDefault_ReturnsADisabledDefinition()
    {
        using var provider = Create();

        var definition = await provider.GetFeatureDefinitionAsync(CapabilityFlag);

        Assert.Equal(FeatureStatus.Disabled, definition.Status);
        Assert.Empty(definition.EnabledFor);
    }

    [Fact]
    public async Task GetFeatureDefinitionAsync_UnknownFlag_ReturnsNull()
    {
        using var provider = Create();

        var definition = await provider.GetFeatureDefinitionAsync("Erp.Modules.Unknown.Module");

        Assert.Null(definition);
    }

    [Fact]
    public async Task GetFeatureDefinitionAsync_SameIdInTwoProviders_LastRegisteredProviderWins()
    {
        using var provider = Create(Flags((CapabilityFlag, true), (ModuleFlag, true)), Flags((ModuleFlag, false)));

        var definition = await provider.GetFeatureDefinitionAsync(ModuleFlag);

        Assert.Equal(FeatureStatus.Disabled, definition.Status);
    }

    [Fact]
    public async Task GetFeatureDefinitionAsync_DifferentIdsAtTheSameIndexInTwoProviders_KeepsBothDefinitions()
    {
        using var provider = Create(Flags((ModuleFlag, false)), Flags((CapabilityFlag, true)));

        var module = await provider.GetFeatureDefinitionAsync(ModuleFlag);
        var capability = await provider.GetFeatureDefinitionAsync(CapabilityFlag);

        Assert.Equal(FeatureStatus.Disabled, module.Status);
        Assert.Equal(FeatureStatus.Conditional, capability.Status);
    }

    [Fact]
    public async Task GetFeatureDefinitionAsync_MicrosoftSchemaInAnEarlierProviderAndDotnetSchemaLater_MicrosoftSchemaWins()
    {
        using var provider = Create(
            Flags((ModuleFlag, false)),
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { [$"FeatureManagement:{ModuleFlag}"] = "true" });

        var definition = await provider.GetFeatureDefinitionAsync(ModuleFlag);

        Assert.Equal(FeatureStatus.Disabled, definition.Status);
    }

    [Fact]
    public async Task GetAllFeatureDefinitionsAsync_MergesConfiguredAndCatalogDefaults()
    {
        using var provider = Create(Flags((CapabilityFlag, true)));

        var definitions = await provider.GetAllFeatureDefinitionsAsync().ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([CapabilityFlag, ModuleFlag], definitions.Select(d => d.Name));
        Assert.All(definitions, d => Assert.Equal(FeatureStatus.Conditional, d.Status));
    }

    private static CatalogFeatureDefinitionProvider Create(params Dictionary<string, string?>[] providers)
    {
        var configuration = new ConfigurationBuilder();
        foreach (var values in providers)
        {
            configuration.AddInMemoryCollection(values);
        }

        var catalog = new FeatureCatalog(new ModuleCatalog([new StubModule("Finance", "Sales", new ModuleCapability("AiDrafts", EnabledByDefault: false))]));
        var options = Options.Create(new ConfigurationFeatureDefinitionProviderOptions { CustomConfigurationMergingEnabled = true });

        return new CatalogFeatureDefinitionProvider(catalog, configuration.Build(), options, NullLogger<CatalogFeatureDefinitionProvider>.Instance);
    }

    private static Dictionary<string, string?> Flags(params (string Name, bool Enabled)[] flags)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < flags.Length; index++)
        {
            values[$"feature_management:feature_flags:{index}:id"] = flags[index].Name;
            values[$"feature_management:feature_flags:{index}:enabled"] = flags[index].Enabled ? "true" : "false";
        }

        return values;
    }
}
