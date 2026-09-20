using Dewiride.Erp.BuildingBlocks.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Modules;

internal sealed class StubModule(string domain, string name, params ModuleCapability[] capabilities) : IModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        domain,
        name,
        null,
        $"/{domain.ToLowerInvariant()}/{name.ToLowerInvariant()}",
        $"Erp.Modules.{domain}.{name}",
        [],
        capabilities);

    public void AddServices(IHostApplicationBuilder builder)
    {
    }

    public void MapEndpoints(RouteGroupBuilder group)
    {
    }
}
