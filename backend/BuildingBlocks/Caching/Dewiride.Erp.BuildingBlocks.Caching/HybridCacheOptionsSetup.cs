using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Caching;

internal sealed class HybridCacheOptionsSetup(IOptions<CachingOptions> options) : IConfigureOptions<HybridCacheOptions>
{
    public void Configure(HybridCacheOptions hybrid)
    {
        var caching = options.Value;
        hybrid.MaximumPayloadBytes = caching.MaximumPayloadBytes;
        hybrid.DefaultEntryOptions = new HybridCacheEntryOptions
        {
            Expiration = caching.DefaultExpiration,
            LocalCacheExpiration = caching.DefaultExpiration,
        };
    }
}
