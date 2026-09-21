using Dewiride.Erp.BuildingBlocks.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Modules;

internal sealed class StubModule(string domain, string name, params ModuleCapability[] capabilities) : IModule
{
    public string? Schema { get; init; }

    public string RoutePrefix { get; init; } = $"/{domain.ToLowerInvariant()}/{name.ToLowerInvariant()}";

    public string FeatureFlag { get; init; } = $"Erp.Modules.{domain}.{name}";

    public IReadOnlyCollection<string> Permissions { get; init; } = [];

    public ModuleDescriptor Descriptor => new(domain, name, Schema, RoutePrefix, FeatureFlag, Permissions, capabilities);

    public void AddServices(IHostApplicationBuilder builder)
    {
    }

    public void MapEndpoints(RouteGroupBuilder group)
    {
    }
}
