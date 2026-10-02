using Microsoft.Extensions.Caching.Distributed;

namespace Dewiride.Erp.Host.Api.IntegrationTests.TokenCache;

// Makes the store under the token cache fail the chosen operations, on every key or on the keys that start with a prefix, as
// SQL Server fails them while it cannot be reached; the message names no key, as a driver error names none.
internal sealed class TokenCacheOutage
{
    public const string FailureMessage = "The token cache store did not answer in time.";

    private Failure? _failure;

    public void Begin(TokenCacheOperations operations, string? keyPrefix = null) => Volatile.Write(ref _failure, new Failure(operations, keyPrefix));

    public void End() => Volatile.Write(ref _failure, null);

    public IDistributedCache Wrap(IDistributedCache store) => new FailingStore(this, store);

    private void ThrowIfFailing(TokenCacheOperations operation, string key)
    {
        if (Volatile.Read(ref _failure) is { } failure
            && (failure.Operations & operation) != 0
            && (failure.KeyPrefix is null || key.StartsWith(failure.KeyPrefix, StringComparison.Ordinal)))
        {
            throw new TimeoutException(FailureMessage);
        }
    }

    private sealed record Failure(TokenCacheOperations Operations, string? KeyPrefix);

    private sealed class FailingStore(TokenCacheOutage outage, IDistributedCache store) : IDistributedCache
    {
        public byte[]? Get(string key)
        {
            outage.ThrowIfFailing(TokenCacheOperations.Read, key);
            return store.Get(key);
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            outage.ThrowIfFailing(TokenCacheOperations.Read, key);
            return store.GetAsync(key, token);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            outage.ThrowIfFailing(TokenCacheOperations.Write, key);
            store.Set(key, value, options);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            outage.ThrowIfFailing(TokenCacheOperations.Write, key);
            return store.SetAsync(key, value, options, token);
        }

        public void Refresh(string key)
        {
            outage.ThrowIfFailing(TokenCacheOperations.Refresh, key);
            store.Refresh(key);
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            outage.ThrowIfFailing(TokenCacheOperations.Refresh, key);
            return store.RefreshAsync(key, token);
        }

        public void Remove(string key)
        {
            outage.ThrowIfFailing(TokenCacheOperations.Remove, key);
            store.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            outage.ThrowIfFailing(TokenCacheOperations.Remove, key);
            return store.RemoveAsync(key, token);
        }
    }
}
