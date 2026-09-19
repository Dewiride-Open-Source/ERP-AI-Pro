using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.Modules;

public interface IModule
{
    ModuleDescriptor Descriptor { get; }

    void AddServices(IHostApplicationBuilder builder);

    void MapEndpoints(RouteGroupBuilder group);
}
