using Dewiride.Erp.BuildingBlocks.Modules;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Modules;

public sealed class ModuleCatalogTests
{
    [Fact]
    public void Constructor_DistinctModules_KeepsThemInOrder()
    {
        IModule[] modules = [new StubModule("Finance", "Sales"), new StubModule("Finance", "Expenses")];

        var catalog = new ModuleCatalog(modules);

        Assert.Equal(["Finance.Sales", "Finance.Expenses"], catalog.Modules.Select(m => m.Descriptor.Id));
    }

    [Fact]
    public void Constructor_DuplicateModuleIds_ThrowsNamingTheModule()
    {
        IModule[] modules = [new StubModule("Finance", "Sales"), new StubModule("Finance", "Sales") { RoutePrefix = "/finance/sales-2", FeatureFlag = "Erp.Modules.Finance.Sales2" }];

        var exception = Assert.Throws<InvalidOperationException>(() => new ModuleCatalog(modules));

        Assert.Contains("Finance.Sales", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_SharedRoutePrefix_ThrowsNamingBothModules()
    {
        IModule[] modules = [new StubModule("Finance", "Sales"), new StubModule("Finance", "Expenses") { RoutePrefix = "/finance/sales" }];

        var exception = Assert.Throws<InvalidOperationException>(() => new ModuleCatalog(modules));

        Assert.Contains("'Finance.Sales' and 'Finance.Expenses'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("route prefix '/finance/sales'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_SharedSchema_ThrowsNamingBothModules()
    {
        IModule[] modules = [new StubModule("Finance", "Sales") { Schema = "finance_sales" }, new StubModule("Finance", "Expenses") { Schema = "FINANCE_SALES" }];

        var exception = Assert.Throws<InvalidOperationException>(() => new ModuleCatalog(modules));

        Assert.Contains("schema 'finance_sales'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_ModulesWithoutSchema_AreNotTreatedAsSharingOne()
    {
        IModule[] modules = [new StubModule("Platform", "SystemInfo"), new StubModule("Platform", "Features")];

        var catalog = new ModuleCatalog(modules);

        Assert.Equal(2, catalog.Modules.Count);
    }

    [Fact]
    public void Constructor_SharedFeatureFlag_ThrowsNamingBothModules()
    {
        IModule[] modules = [new StubModule("Finance", "Sales"), new StubModule("Finance", "Expenses") { FeatureFlag = "Erp.Modules.Finance.Sales" }];

        var exception = Assert.Throws<InvalidOperationException>(() => new ModuleCatalog(modules));

        Assert.Contains("feature flag 'Erp.Modules.Finance.Sales'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_PermissionOutsideTheModulePrefix_ThrowsNamingTheModule()
    {
        IModule[] modules = [new StubModule("Finance", "Sales") { Permissions = ["finance.sales.invoices.issue", "finance.expenses.bills.approve"] }];

        var exception = Assert.Throws<InvalidOperationException>(() => new ModuleCatalog(modules));

        Assert.Contains("'Finance.Sales'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'finance.expenses.bills.approve'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'finance.sales.'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_PermissionsWithTheModulePrefix_AreAccepted()
    {
        IModule[] modules = [new StubModule("Finance", "Sales") { Permissions = ["finance.sales.invoices.issue", "finance.sales.invoices.read"] }];

        var catalog = new ModuleCatalog(modules);

        Assert.Equal(2, catalog.Modules[0].Descriptor.Permissions.Count);
    }

    [Fact]
    public void Descriptor_Id_CombinesDomainAndName()
    {
        var descriptor = new StubModule("Platform", "SystemInfo").Descriptor;

        Assert.Equal("Platform.SystemInfo", descriptor.Id);
    }

    [Fact]
    public void Descriptor_PermissionPrefix_DerivesFromTheRoutePrefix()
    {
        var descriptor = new StubModule("Platform", "SystemInfo") { RoutePrefix = "/platform/system-info" }.Descriptor;

        Assert.Equal("platform.system-info.", descriptor.PermissionPrefix);
    }
}
