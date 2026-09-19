using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.Modules;

public static class ModuleRegistration
{
    public static IHostApplicationBuilder AddModules(this IHostApplicationBuilder builder, IReadOnlyList<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var catalog = new ModuleCatalog(modules);
        builder.Services.AddSingleton(catalog);

        foreach (var module in catalog.Modules)
        {
            module.AddServices(builder);
        }

        return builder;
    }

    public static RouteGroupBuilder MapModules(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var catalog = endpoints.ServiceProvider.GetRequiredService<ModuleCatalog>();
        var api = endpoints.MapGroup("/api");

        foreach (var module in catalog.Modules)
        {
            var descriptor = module.Descriptor;
            var group = api.MapGroup(descriptor.RoutePrefix).WithTags(descriptor.Id);
            module.MapEndpoints(group);
        }

        return api;
    }
}
