using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.BuildingBlocks.Modules.Features;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Modules.Features;

public sealed class FeatureCatalogTests
{
    [Fact]
    public void Constructor_ModuleWithoutCapabilities_AddsOneEnabledModuleFlag()
    {
        var catalog = new FeatureCatalog(new ModuleCatalog([new StubModule("Platform", "SystemInfo")]));

        var feature = Assert.Single(catalog.Features);
        Assert.Equal(new FeatureDescriptor("Erp.Modules.Platform.SystemInfo", EnabledByDefault: true), feature);
    }

    [Fact]
    public void Constructor_ModuleWithCapabilities_AddsCapabilityFlagsWithTheDeclaredDefaults()
    {
        var module = new StubModule("Finance", "Sales", new ModuleCapability("AiDrafts", EnabledByDefault: false), new ModuleCapability("Reminders", EnabledByDefault: true));

        var catalog = new FeatureCatalog(new ModuleCatalog([module]));

        Assert.Equal(
            [
                new FeatureDescriptor("Erp.Modules.Finance.Sales", true),
                new FeatureDescriptor("Erp.Modules.Finance.Sales.AiDrafts", false),
                new FeatureDescriptor("Erp.Modules.Finance.Sales.Reminders", true),
            ],
            catalog.Features);
    }

    [Fact]
    public void Constructor_DuplicateFlagNames_ThrowsInvalidOperation()
    {
        var module = new StubModule("Finance", "Sales", new ModuleCapability("Reminders", true), new ModuleCapability("reminders", false));

        var exception = Assert.Throws<InvalidOperationException>(() => new FeatureCatalog(new ModuleCatalog([module])));

        Assert.Contains("Erp.Modules.Finance.Sales.reminders", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_FlagNameWithColon_ThrowsInvalidOperation()
    {
        var module = new StubModule("Finance", "Sales", new ModuleCapability("Ai:Drafts", true));

        var exception = Assert.Throws<InvalidOperationException>(() => new FeatureCatalog(new ModuleCatalog([module])));

        Assert.Contains("must not contain ':'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryGet_DifferentCasing_FindsTheFlag()
    {
        var catalog = new FeatureCatalog(new ModuleCatalog([new StubModule("Platform", "SystemInfo")]));

        Assert.True(catalog.TryGet("erp.modules.platform.systeminfo", out var feature));
        Assert.Equal("Erp.Modules.Platform.SystemInfo", feature.Name);
        Assert.False(catalog.TryGet("Erp.Modules.Finance.Sales", out _));
    }
}
