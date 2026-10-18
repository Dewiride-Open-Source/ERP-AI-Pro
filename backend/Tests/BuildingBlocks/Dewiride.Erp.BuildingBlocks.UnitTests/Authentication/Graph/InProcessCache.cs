using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.Graph;

// The cache the hosts register, in process only and of its own per test, so no test sees what another kept.
internal sealed class InProcessCache : IDisposable
{
    private readonly ServiceProvider _services;

    public InProcessCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        _services = services.BuildServiceProvider();
        Cache = _services.GetRequiredService<HybridCache>();
    }

    public HybridCache Cache { get; }

    public void Dispose() => _services.Dispose();
}
