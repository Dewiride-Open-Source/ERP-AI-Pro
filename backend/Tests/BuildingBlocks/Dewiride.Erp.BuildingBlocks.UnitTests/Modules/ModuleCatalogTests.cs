using Dewiride.Erp.BuildingBlocks.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Modules;

public sealed class ModuleCatalogTests
{
    [Fact]
    public void Constructor_KeepsDistinctModulesInOrder()
    {
        IModule[] modules = [new StubModule("Finance", "Sales"), new StubModule("Finance", "Expenses")];

        var catalog = new ModuleCatalog(modules);

        Assert.Equal(["Finance.Sales", "Finance.Expenses"], catalog.Modules.Select(m => m.Descriptor.Id));
    }

    [Fact]
    public void Constructor_RejectsDuplicateModuleIds()
    {
        IModule[] modules = [new StubModule("Finance", "Sales"), new StubModule("Finance", "Sales")];

        var exception = Assert.Throws<InvalidOperationException>(() => new ModuleCatalog(modules));

        Assert.Contains("Finance.Sales", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Descriptor_IdCombinesDomainAndName()
    {
        var descriptor = new StubModule("Platform", "SystemInfo").Descriptor;

        Assert.Equal("Platform.SystemInfo", descriptor.Id);
    }

    private sealed class StubModule(string domain, string name) : IModule
    {
        public ModuleDescriptor Descriptor { get; } = new(domain, name, null, $"/{domain.ToLowerInvariant()}/{name.ToLowerInvariant()}", $"Erp.Modules.{domain}.{name}", []);

        public void AddServices(IHostApplicationBuilder builder)
        {
        }

        public void MapEndpoints(RouteGroupBuilder group)
        {
        }
    }
}
