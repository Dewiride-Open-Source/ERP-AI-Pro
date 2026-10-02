using Dewiride.Erp.BuildingBlocks.Caching;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Caching;

// SqlClient's own providers recognise only a SqlException, which has no public constructor, so these tests retry through a
// provider that applies the same rules to a TimeoutException: Retrying before each retry, and an AggregateException holding
// every failure once the tries run out. RetryingSqlServerCacheTests in the integration tests retries real SQL Server errors.
public sealed class RetryingSqlServerCacheTests
{
    private const string Key = "retrying-cache-test";

    private const int Tries = 3;

    private static readonly byte[] Value = [1, 2, 3];

    private static readonly DistributedCacheEntryOptions Expiry = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };

    private readonly FakeLogger<RetryingSqlServerCache> _logger = new();

    [Fact]
    public async Task GetAsync_TransientFailureThatClearsOnTheSecondAttempt_ReturnsTheStoredValue()
    {
        var store = new FlakyStore(failures: 1);
        await store.Inner.SetAsync(Key, Value, Expiry, TestContext.Current.CancellationToken);

        var value = await Cache(store).GetAsync(Key, TestContext.Current.CancellationToken);

        Assert.Equal(Value, value);
        Assert.Equal(2, store.Attempts);
    }

    [Fact]
    public void Get_TransientFailureThatClearsOnTheSecondAttempt_ReturnsTheStoredValue()
    {
        var store = new FlakyStore(failures: 1);
        store.Inner.Set(Key, Value, Expiry);

        var value = Cache(store).Get(Key);

        Assert.Equal(Value, value);
        Assert.Equal(2, store.Attempts);
    }

    [Fact]
    public async Task SetAsyncAndSet_TransientFailureThatClearsOnTheSecondAttempt_StoreTheValue()
    {
        var asyncStore = new FlakyStore(failures: 1);
        var syncStore = new FlakyStore(failures: 1);

        await Cache(asyncStore).SetAsync(Key, Value, Expiry, TestContext.Current.CancellationToken);
        Cache(syncStore).Set(Key, Value, Expiry);

        Assert.Equal(Value, await asyncStore.Inner.GetAsync(Key, TestContext.Current.CancellationToken));
        Assert.Equal(Value, syncStore.Inner.Get(Key));
        Assert.Equal(2, asyncStore.Attempts);
        Assert.Equal(2, syncStore.Attempts);
    }

    [Fact]
    public async Task RemoveAsyncAndRemove_TransientFailureThatClearsOnTheSecondAttempt_RemoveTheValue()
    {
        var asyncStore = new FlakyStore(failures: 1);
        var syncStore = new FlakyStore(failures: 1);
        await asyncStore.Inner.SetAsync(Key, Value, Expiry, TestContext.Current.CancellationToken);
        syncStore.Inner.Set(Key, Value, Expiry);

        await Cache(asyncStore).RemoveAsync(Key, TestContext.Current.CancellationToken);
        Cache(syncStore).Remove(Key);

        Assert.Null(await asyncStore.Inner.GetAsync(Key, TestContext.Current.CancellationToken));
        Assert.Null(syncStore.Inner.Get(Key));
        Assert.Equal(2, asyncStore.Attempts);
        Assert.Equal(2, syncStore.Attempts);
    }

    [Fact]
    public async Task RefreshAsyncAndRefresh_TransientFailureThatClearsOnTheSecondAttempt_ReachTheStoreAgain()
    {
        var asyncStore = new FlakyStore(failures: 1);
        var syncStore = new FlakyStore(failures: 1);

        await Cache(asyncStore).RefreshAsync(Key, TestContext.Current.CancellationToken);
        Cache(syncStore).Refresh(Key);

        Assert.Equal(2, asyncStore.Attempts);
        Assert.Equal(2, syncStore.Attempts);
    }

    [Fact]
    public async Task GetAsync_NonTransientFailure_PropagatesItWithoutAnotherAttempt()
    {
        var failure = new InvalidOperationException("The cache table does not exist.");
        var store = new FlakyStore(failures: int.MaxValue, failure);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Cache(store).GetAsync(Key, TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
        Assert.Equal(1, store.Attempts);
        Assert.Empty(_logger.Collector.GetSnapshot());
    }

    [Fact]
    public void Set_NonTransientFailure_PropagatesItWithoutAnotherAttempt()
    {
        var failure = new InvalidOperationException("The cache table does not exist.");
        var store = new FlakyStore(failures: int.MaxValue, failure);

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => Cache(store).Set(Key, Value, Expiry)));
        Assert.Equal(1, store.Attempts);
    }

    [Fact]
    public async Task SetAsync_TransientFailureOnEveryAttempt_PropagatesTheFailureOfEveryAttemptOnceTheTriesRunOut()
    {
        var store = new FlakyStore(failures: int.MaxValue);

        var thrown = await Assert.ThrowsAsync<AggregateException>(() => Cache(store).SetAsync(Key, Value, Expiry, TestContext.Current.CancellationToken));

        Assert.Equal(Tries, thrown.InnerExceptions.Count);
        Assert.All(thrown.InnerExceptions, failure => Assert.IsType<TimeoutException>(failure));
        Assert.Equal(Tries, store.Attempts);
    }

    [Fact]
    public void Remove_TransientFailureOnEveryAttempt_PropagatesTheFailureOfEveryAttemptOnceTheTriesRunOut()
    {
        var store = new FlakyStore(failures: int.MaxValue);

        var thrown = Assert.Throws<AggregateException>(() => Cache(store).Remove(Key));

        Assert.Equal(Tries, thrown.InnerExceptions.Count);
        Assert.Equal(Tries, store.Attempts);
    }

    [Fact]
    public async Task GetAsync_TransientFailuresBeforeSuccess_LogAWarningCarryingTheFailureForEachRetry()
    {
        var store = new FlakyStore(failures: 2);

        await Cache(store).GetAsync(Key, TestContext.Current.CancellationToken);

        var records = _logger.Collector.GetSnapshot();
        Assert.Equal(2, records.Count);
        Assert.All(records, record =>
        {
            Assert.Equal(LogLevel.Warning, record.Level);
            Assert.IsType<TimeoutException>(record.Exception);
        });
        Assert.Equal(["1", "2"], records.Select(record => record.GetStructuredStateValue("RetryCount")));
    }

    [Fact]
    public async Task GetAsyncSetAsyncRefreshAsyncAndRemoveAsync_CancellationToken_ReachesTheStore()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new FlakyStore(failures: 0);
        var cache = Cache(store);

        await cache.GetAsync(Key, cancellation.Token);
        await cache.SetAsync(Key, Value, Expiry, cancellation.Token);
        await cache.RefreshAsync(Key, cancellation.Token);
        await cache.RemoveAsync(Key, cancellation.Token);

        Assert.Equal(4, store.Tokens.Count);
        Assert.All(store.Tokens, token => Assert.Equal(cancellation.Token, token));
    }

    private RetryingSqlServerCache Cache(FlakyStore store) => new(store, new TimeoutRetryProvider(Tries), _logger);

    private sealed class TimeoutRetryProvider : SqlRetryLogicBaseProvider
    {
        public TimeoutRetryProvider(int numberOfTries)
        {
            RetryLogic = new TimeoutRetryLogic(numberOfTries);
        }

        public override TResult Execute<TResult>(object sender, Func<TResult> function)
        {
            var failures = new List<Exception>();
            while (true)
            {
                try
                {
                    return function();
                }
                catch (Exception exception) when (RetryLogic.TransientPredicate(exception))
                {
                    RetryOrThrow(sender, failures, exception);
                }
            }
        }

        public override async Task<TResult> ExecuteAsync<TResult>(object sender, Func<Task<TResult>> function, CancellationToken cancellationToken = default)
        {
            var failures = new List<Exception>();
            while (true)
            {
                try
                {
                    return await function();
                }
                catch (Exception exception) when (RetryLogic.TransientPredicate(exception))
                {
                    RetryOrThrow(sender, failures, exception);
                }
            }
        }

        public override Task ExecuteAsync(object sender, Func<Task> function, CancellationToken cancellationToken = default) =>
            ExecuteAsync(sender, async () =>
            {
                await function();
                return true;
            }, cancellationToken);

        private void RetryOrThrow(object sender, List<Exception> failures, Exception exception)
        {
            failures.Add(exception);
            if (failures.Count >= RetryLogic.NumberOfTries)
            {
                throw new AggregateException(failures);
            }

            Retrying?.Invoke(sender, new SqlRetryingEventArgs(failures.Count, TimeSpan.Zero, failures));
        }
    }

    private sealed class TimeoutRetryLogic : SqlRetryLogicBase
    {
        public TimeoutRetryLogic(int numberOfTries)
        {
            NumberOfTries = numberOfTries;
            TransientPredicate = exception => exception is TimeoutException;
        }

        public override bool TryNextInterval(out TimeSpan intervalTime)
        {
            intervalTime = TimeSpan.Zero;
            return Current++ < NumberOfTries - 1;
        }

        public override void Reset() => Current = 0;
    }

    private sealed class FlakyStore(int failures, Exception? failure = null) : IDistributedCache
    {
        private int _remainingFailures = failures;

        public MemoryDistributedCache Inner { get; } = new(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));

        public int Attempts { get; private set; }

        public List<CancellationToken> Tokens { get; } = [];

        public byte[]? Get(string key)
        {
            Attempt();
            return Inner.Get(key);
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            Attempt(token);
            return Inner.GetAsync(key, token);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            Attempt();
            Inner.Set(key, value, options);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Attempt(token);
            return Inner.SetAsync(key, value, options, token);
        }

        public void Refresh(string key)
        {
            Attempt();
            Inner.Refresh(key);
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            Attempt(token);
            return Inner.RefreshAsync(key, token);
        }

        public void Remove(string key)
        {
            Attempt();
            Inner.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Attempt(token);
            return Inner.RemoveAsync(key, token);
        }

        private void Attempt(CancellationToken? token = null)
        {
            Attempts++;
            if (token is { } passed)
            {
                Tokens.Add(passed);
            }

            if (_remainingFailures > 0)
            {
                _remainingFailures--;
                throw failure ?? new TimeoutException("The SQL Server cache did not answer in time.");
            }
        }
    }
}
