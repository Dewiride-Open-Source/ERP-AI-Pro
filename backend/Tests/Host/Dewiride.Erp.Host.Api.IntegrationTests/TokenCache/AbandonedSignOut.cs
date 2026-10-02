using Dewiride.Erp.BuildingBlocks.Authentication.Sessions;
using Microsoft.Extensions.Caching.Distributed;

namespace Dewiride.Erp.Host.Api.IntegrationTests.TokenCache;

// Cancels the browser's request the moment the store receives the person's sign-out record, as a browser closed during the
// sign-out would, and reports how writing that record ended.
internal sealed class AbandonedSignOut(CancellationTokenSource browser)
{
    private readonly TaskCompletionSource _recorded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Recorded => _recorded.Task;

    public IDistributedCache Wrap(IDistributedCache store) => new AbandoningStore(this, store);

    private async Task AbandonWhileRecordingAsync(Func<Task> record)
    {
        await browser.CancelAsync();
        try
        {
            await record();
            _recorded.TrySetResult();
        }
        catch (Exception exception)
        {
            _recorded.TrySetException(exception);
            throw;
        }
    }

    private sealed class AbandoningStore(AbandonedSignOut signOut, IDistributedCache store) : IDistributedCache
    {
        public byte[]? Get(string key) => store.Get(key);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => store.GetAsync(key, token);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => store.Set(key, value, options);

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            key.StartsWith(SessionRevocations.KeyPrefix, StringComparison.Ordinal)
                ? signOut.AbandonWhileRecordingAsync(() => store.SetAsync(key, value, options, token))
                : store.SetAsync(key, value, options, token);

        public void Refresh(string key) => store.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) => store.RefreshAsync(key, token);

        public void Remove(string key) => store.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default) => store.RemoveAsync(key, token);
    }
}
