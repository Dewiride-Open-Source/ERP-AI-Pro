using Microsoft.Extensions.Caching.Distributed;

namespace Dewiride.Erp.Testing.Deployment;

internal sealed class PrefixedDistributedCache(string prefix, IDistributedCache store) : IDistributedCache
{
    public byte[]? Get(string key) => store.Get(prefix + key);

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => store.GetAsync(prefix + key, token);

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => store.Set(prefix + key, value, options);

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
        store.SetAsync(prefix + key, value, options, token);

    public void Refresh(string key) => store.Refresh(prefix + key);

    public Task RefreshAsync(string key, CancellationToken token = default) => store.RefreshAsync(prefix + key, token);

    public void Remove(string key) => store.Remove(prefix + key);

    public Task RemoveAsync(string key, CancellationToken token = default) => store.RemoveAsync(prefix + key, token);
}
