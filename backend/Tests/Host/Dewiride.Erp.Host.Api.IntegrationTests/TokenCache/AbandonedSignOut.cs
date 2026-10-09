using Microsoft.Extensions.Caching.Distributed;

namespace Dewiride.Erp.Host.Api.IntegrationTests.TokenCache;

// Cancels the browser's request the moment the store receives the chosen write or removal of a key with the chosen prefix,
// as a browser closed during a sign-out would, and reports how that operation ended.
internal sealed class AbandonedSignOut(CancellationTokenSource browser, TokenCacheOperations operation, string keyPrefix)
{
    private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Completed => _completed.Task;

    public IDistributedCache Wrap(IDistributedCache store) => new AbandoningStore(this, store);

    private bool Abandons(TokenCacheOperations received, string key) =>
        received == operation && key.StartsWith(keyPrefix, StringComparison.Ordinal);

    private async Task AbandonWhileStoringAsync(Func<Task> store)
    {
        await browser.CancelAsync();
        try
        {
            await store();
            _completed.TrySetResult();
        }
        catch (Exception exception)
        {
            _completed.TrySetException(exception);
            throw;
        }
    }

    private sealed class AbandoningStore(AbandonedSignOut signOut, IDistributedCache store) : IDistributedCache
    {
        public byte[]? Get(string key) => store.Get(key);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => store.GetAsync(key, token);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => store.Set(key, value, options);

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            signOut.Abandons(TokenCacheOperations.Write, key)
                ? signOut.AbandonWhileStoringAsync(() => store.SetAsync(key, value, options, token))
                : store.SetAsync(key, value, options, token);

        public void Refresh(string key) => store.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) => store.RefreshAsync(key, token);

        public void Remove(string key) => store.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default) =>
            signOut.Abandons(TokenCacheOperations.Remove, key)
                ? signOut.AbandonWhileStoringAsync(() => store.RemoveAsync(key, token))
                : store.RemoveAsync(key, token);
    }
}
