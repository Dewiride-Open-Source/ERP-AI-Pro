using System.Security.Cryptography;
using System.Text;
using Dewiride.Erp.BuildingBlocks.Authentication.TokenCache;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.TokenCache;

public sealed class ProtectedTokenCacheStoreTests
{
    private const string AccountId = "3f0c2a8e-6b1d-4c7e-9a52-0d8e4f1b7c31.5d7c3b9a-1e2f-4a6b-8c0d-9e8f7a6b5c4d";

    private const string OtherAccountId = "8b6e1f47-2c9a-4d3b-b5e0-7a19c4d2e865.5d7c3b9a-1e2f-4a6b-8c0d-9e8f7a6b5c4d";

    private static readonly byte[] Entry = Encoding.UTF8.GetBytes("""{"RefreshToken":{"secret":"refresh-token-value"}}""");

    private static readonly DistributedCacheEntryOptions Expiry = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };

    private readonly MemoryDistributedCache _inner = new(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));

    private readonly EphemeralDataProtectionProvider _dataProtection = new();

    private readonly HttpContextAccessor _httpContext = new();

    private readonly FakeLogger<ProtectedTokenCacheStore> _logger = new();

    [Fact]
    public async Task SetAsyncThenGetAsync_WithTheSameKeyRing_ReturnsTheEntryAndStoresOnlyItsProtectedForm()
    {
        var store = Store();

        await store.SetAsync(AccountId, Entry, Expiry, TestContext.Current.CancellationToken);

        Assert.Equal(Entry, await store.GetAsync(AccountId, TestContext.Current.CancellationToken));
        var stored = await _inner.GetAsync(AccountId, TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.NotEqual(Entry, stored);
        Assert.True(stored.AsSpan().IndexOf("refresh-token-value"u8) < 0);
        Assert.Equal(Entry, _dataProtection.CreateProtector(ProtectedTokenCacheStore.Purpose).Unprotect(stored));
    }

    [Fact]
    public void SetThenGet_WithTheSameKeyRing_ReturnsTheEntry()
    {
        var store = Store();

        store.Set(AccountId, Entry, Expiry);

        Assert.Equal(Entry, store.Get(AccountId));
    }

    [Fact]
    public async Task GetAsync_EntryWrittenUnderAnotherKeyRing_ReadsAsMissingAndWarnsWithoutTheKey()
    {
        var foreign = new EphemeralDataProtectionProvider().CreateProtector(ProtectedTokenCacheStore.Purpose).Protect(Entry);
        await _inner.SetAsync(AccountId, foreign, Expiry, TestContext.Current.CancellationToken);

        var entry = await Store().GetAsync(AccountId, TestContext.Current.CancellationToken);

        Assert.Null(entry);
        var warning = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.DoesNotContain(AccountId, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Get_EntryThatIsNoProtectedPayload_ReadsAsMissing()
    {
        _inner.Set(AccountId, Entry, Expiry);

        Assert.Null(Store().Get(AccountId));
        Assert.Equal(LogLevel.Warning, Assert.Single(_logger.Collector.GetSnapshot()).Level);
    }

    [Fact]
    public async Task GetAsync_MissingEntry_ReturnsNullWithoutAWarning()
    {
        Assert.Null(await Store().GetAsync(AccountId, TestContext.Current.CancellationToken));
        Assert.Empty(_logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task RemoveAsync_StoredEntry_RemovesItFromTheInnerStore()
    {
        var store = Store();
        await store.SetAsync(AccountId, Entry, Expiry, TestContext.Current.CancellationToken);

        await store.RemoveAsync(AccountId, TestContext.Current.CancellationToken);

        Assert.Null(await _inner.GetAsync(AccountId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Remove_StoredEntry_RemovesItFromTheInnerStore()
    {
        var store = Store();
        store.Set(AccountId, Entry, Expiry);

        store.Remove(AccountId);

        Assert.Null(_inner.Get(AccountId));
    }

    [Fact]
    public async Task RefreshAsync_SlidingEntry_PassesTheRefreshToTheInnerStore()
    {
        var inner = new RecordingStore();
        var store = new ProtectedTokenCacheStore(inner, _dataProtection, _httpContext, _logger);

        await store.RefreshAsync(AccountId, TestContext.Current.CancellationToken);
        store.Refresh(AccountId);

        Assert.Equal([$"RefreshAsync {AccountId}", $"Refresh {AccountId}"], inner.Calls);
    }

    [Fact]
    public async Task GetAsync_InnerStoreFailing_PropagatesTheFailure()
    {
        var failure = new TimeoutException("The store did not answer.");
        var store = new ProtectedTokenCacheStore(new RecordingStore(failure), _dataProtection, _httpContext, _logger);

        Assert.Same(failure, await Assert.ThrowsAsync<TimeoutException>(() => store.GetAsync(AccountId, TestContext.Current.CancellationToken)));
        Assert.Same(failure, await Assert.ThrowsAsync<TimeoutException>(() => store.SetAsync(AccountId, Entry, Expiry, TestContext.Current.CancellationToken)));
        Assert.Same(failure, await Assert.ThrowsAsync<TimeoutException>(() => store.RemoveAsync(AccountId, TestContext.Current.CancellationToken)));
        Assert.Same(failure, Assert.Throws<TimeoutException>(() => store.Get(AccountId)));
        Assert.Empty(_logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task GetAsync_WhenTheKeyRingCannotBeReached_PropagatesTheFailureInsteadOfReadingAsMissing()
    {
        var cause = new IOException("The key ring could not be read.");
        var store = new ProtectedTokenCacheStore(_inner, new UnreachableKeyRing(cause), _httpContext, _logger);
        await _inner.SetAsync(AccountId, Entry, Expiry, TestContext.Current.CancellationToken);

        var failure = await Assert.ThrowsAsync<CryptographicException>(() => store.GetAsync(AccountId, TestContext.Current.CancellationToken));

        Assert.Same(cause, failure.InnerException);
        Assert.Throws<CryptographicException>(() => store.Get(AccountId));
        Assert.Empty(_logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task RemoveAsync_OfAKeyWhoseReadFailedInTheSameRequest_FailsWithoutNamingTheKeyAndKeepsTheEntry()
    {
        var inner = new RecordingStore(readFailure: new TimeoutException("The store did not answer."));
        var store = new ProtectedTokenCacheStore(inner, _dataProtection, _httpContext, _logger);
        _httpContext.HttpContext = new DefaultHttpContext();
        await Assert.ThrowsAsync<TimeoutException>(() => store.GetAsync(AccountId, TestContext.Current.CancellationToken));

        var removal = await Assert.ThrowsAsync<InvalidOperationException>(() => store.RemoveAsync(AccountId, TestContext.Current.CancellationToken));
        var syncRemoval = Assert.Throws<InvalidOperationException>(() => store.Remove(AccountId));

        Assert.Equal(ProtectedTokenCacheStore.ChangeAfterFailedReadMessage, removal.Message);
        Assert.Equal(ProtectedTokenCacheStore.ChangeAfterFailedReadMessage, syncRemoval.Message);
        Assert.DoesNotContain(AccountId, removal.Message, StringComparison.Ordinal);
        Assert.Empty(inner.Calls);
    }

    [Fact]
    public async Task SetAsync_OfAKeyWhoseReadFailedInTheSameRequest_FailsAndKeepsTheEntry()
    {
        var inner = new RecordingStore(readFailure: new TimeoutException("The store did not answer."));
        var store = new ProtectedTokenCacheStore(inner, _dataProtection, _httpContext, _logger);
        _httpContext.HttpContext = new DefaultHttpContext();
        Assert.Throws<TimeoutException>(() => store.Get(AccountId));

        var write = await Assert.ThrowsAsync<InvalidOperationException>(() => store.SetAsync(AccountId, Entry, Expiry, TestContext.Current.CancellationToken));
        var syncWrite = Assert.Throws<InvalidOperationException>(() => store.Set(AccountId, Entry, Expiry));

        Assert.Equal(ProtectedTokenCacheStore.ChangeAfterFailedReadMessage, write.Message);
        Assert.Equal(ProtectedTokenCacheStore.ChangeAfterFailedReadMessage, syncWrite.Message);
        Assert.Empty(inner.Calls);
    }

    [Fact]
    public async Task GetAsync_WhenTheReadFails_PropagatesTheReadsOwnExceptionWithItsStack()
    {
        var failure = new TimeoutException("The store did not answer.");
        var store = new ProtectedTokenCacheStore(new RecordingStore(readFailure: failure), _dataProtection, _httpContext, _logger);
        _httpContext.HttpContext = new DefaultHttpContext();

        var thrown = await Assert.ThrowsAsync<TimeoutException>(() => store.GetAsync(AccountId, TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
        Assert.Contains(nameof(RecordingStore), thrown.StackTrace, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveAsync_OfAnotherKeyAfterAFailedRead_RemovesIt()
    {
        var inner = new RecordingStore(readFailure: new TimeoutException("The store did not answer."));
        var store = new ProtectedTokenCacheStore(inner, _dataProtection, _httpContext, _logger);
        _httpContext.HttpContext = new DefaultHttpContext();
        await Assert.ThrowsAsync<TimeoutException>(() => store.GetAsync(AccountId, TestContext.Current.CancellationToken));

        await store.RemoveAsync(OtherAccountId, TestContext.Current.CancellationToken);

        Assert.Equal([$"RemoveAsync {OtherAccountId}"], inner.Calls);
    }

    [Fact]
    public async Task RemoveAsync_InALaterRequestThanTheFailedRead_RemovesTheKey()
    {
        var inner = new RecordingStore(readFailure: new TimeoutException("The store did not answer."));
        var store = new ProtectedTokenCacheStore(inner, _dataProtection, _httpContext, _logger);
        _httpContext.HttpContext = new DefaultHttpContext();
        await Assert.ThrowsAsync<TimeoutException>(() => store.GetAsync(AccountId, TestContext.Current.CancellationToken));

        _httpContext.HttpContext = new DefaultHttpContext();
        await store.RemoveAsync(AccountId, TestContext.Current.CancellationToken);
        await store.SetAsync(AccountId, Entry, Expiry, TestContext.Current.CancellationToken);

        Assert.Equal([$"RemoveAsync {AccountId}", $"SetAsync {AccountId}"], inner.Calls);
    }

    [Fact]
    public async Task RemoveAsync_OutsideARequestAfterAFailedRead_RemovesTheKey()
    {
        var inner = new RecordingStore(readFailure: new TimeoutException("The store did not answer."));
        var store = new ProtectedTokenCacheStore(inner, _dataProtection, _httpContext, _logger);
        await Assert.ThrowsAsync<TimeoutException>(() => store.GetAsync(AccountId, TestContext.Current.CancellationToken));

        await store.RemoveAsync(AccountId, TestContext.Current.CancellationToken);

        Assert.Equal([$"RemoveAsync {AccountId}"], inner.Calls);
    }

    [Fact]
    public async Task RemoveAsync_AfterAnEntryNoKeyOpensReadAsMissing_RemovesTheKey()
    {
        var foreign = new EphemeralDataProtectionProvider().CreateProtector(ProtectedTokenCacheStore.Purpose).Protect(Entry);
        await _inner.SetAsync(AccountId, foreign, Expiry, TestContext.Current.CancellationToken);
        var store = Store();
        _httpContext.HttpContext = new DefaultHttpContext();
        Assert.Null(await store.GetAsync(AccountId, TestContext.Current.CancellationToken));

        await store.RemoveAsync(AccountId, TestContext.Current.CancellationToken);

        Assert.Null(await _inner.GetAsync(AccountId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EveryAsyncOperation_WithATokenThatCannotBeCancelled_HandsTheStoreTheTokenOfTheRequest()
    {
        using var request = new CancellationTokenSource();
        _httpContext.HttpContext = new DefaultHttpContext { RequestAborted = request.Token };
        var inner = new TokenRecordingStore();
        var store = new ProtectedTokenCacheStore(inner, _dataProtection, _httpContext, _logger);

        await store.SetAsync(AccountId, Entry, Expiry, CancellationToken.None);
        await store.GetAsync(AccountId, CancellationToken.None);
        await store.RefreshAsync(AccountId, CancellationToken.None);
        await store.RemoveAsync(AccountId, CancellationToken.None);

        Assert.Equal(4, inner.Tokens.Count);
        Assert.All(inner.Tokens, token => Assert.Equal(request.Token, token));
    }

    [Fact]
    public async Task GetAsync_WithACancellableToken_HandsTheStoreThatToken()
    {
        using var request = new CancellationTokenSource();
        using var caller = new CancellationTokenSource();
        _httpContext.HttpContext = new DefaultHttpContext { RequestAborted = request.Token };
        var inner = new TokenRecordingStore();

        await new ProtectedTokenCacheStore(inner, _dataProtection, _httpContext, _logger).GetAsync(AccountId, caller.Token);

        Assert.Equal(caller.Token, Assert.Single(inner.Tokens));
    }

    private ProtectedTokenCacheStore Store() => new(_inner, _dataProtection, _httpContext, _logger);

    private sealed class TokenRecordingStore : IDistributedCache
    {
        public List<CancellationToken> Tokens { get; } = [];

        public byte[]? Get(string key) => null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            Tokens.Add(token);
            return Task.FromResult<byte[]?>(null);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Tokens.Add(token);
            return Task.CompletedTask;
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            Tokens.Add(token);
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Tokens.Add(token);
            return Task.CompletedTask;
        }
    }

    private sealed class UnreachableKeyRing(Exception cause) : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) => plaintext;

        public byte[] Unprotect(byte[] protectedData) => throw new CryptographicException("The provided payload could not be decrypted.", cause);
    }

    private sealed class RecordingStore(Exception? failure = null, Exception? readFailure = null) : IDistributedCache
    {
        public List<string> Calls { get; } = [];

        public byte[]? Get(string key) => Read($"Get {key}");

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Read($"GetAsync {key}"));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => Record($"Set {key}", value);

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Record($"SetAsync {key}", value);
            return Task.CompletedTask;
        }

        public void Refresh(string key) => Record($"Refresh {key}", 0);

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            Record($"RefreshAsync {key}", 0);
            return Task.CompletedTask;
        }

        public void Remove(string key) => Record($"Remove {key}", 0);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Record($"RemoveAsync {key}", 0);
            return Task.CompletedTask;
        }

        private byte[]? Read(string call)
        {
            if (readFailure is not null)
            {
                throw readFailure;
            }

            return Record<byte[]?>(call, null);
        }

        private T Record<T>(string call, T result)
        {
            if (failure is not null)
            {
                throw failure;
            }

            Calls.Add(call);
            return result;
        }
    }
}
