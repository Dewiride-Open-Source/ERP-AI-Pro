using Dewiride.Erp.BuildingBlocks.Caching;
using Dewiride.Erp.BuildingBlocks.Caching.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.Testing.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.Logging.Testing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Caching;

// SQL Server answers a login to a database that does not exist with error 4060, one of SqlClient's baseline transient errors,
// so the product's retry provider retries a real SqlException here. One retry with a one-second ceiling keeps the test short.
public sealed class RetryingSqlServerCacheTests
{
    private const int CannotOpenDatabase = 4060;

    private static readonly DatabaseOptions OneRetry = new() { MaxRetryCount = 1, MaxRetryDelay = TimeSpan.FromSeconds(1) };

    [Fact]
    public async Task GetAsync_DatabaseThatCannotBeOpened_RetriesTheTransientErrorAndReportsTheFailureOfEveryAttempt()
    {
        var logger = new FakeLogger<RetryingSqlServerCache>();
        var cache = new RetryingSqlServerCache(StoreOverAMissingDatabase(), RetryingSqlServerCache.CreateRetryProvider(OneRetry), logger);

        var failure = await Assert.ThrowsAsync<AggregateException>(() => cache.GetAsync(NewKey(), TestContext.Current.CancellationToken));

        Assert.Equal(OneRetry.MaxRetryCount + 1, failure.InnerExceptions.Count);
        Assert.All(failure.InnerExceptions, attempt => Assert.Contains(Assert.IsType<SqlException>(attempt).Errors.Cast<SqlError>(), error => error.Number == CannotOpenDatabase));
        var retry = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, retry.Level);
        Assert.IsType<SqlException>(retry.Exception);
    }

    [Fact]
    public async Task GetAsync_CancelledWhileWaitingToRetry_StopsWithoutAnotherAttempt()
    {
        using var cancellation = new CancellationTokenSource();
        var retry = RetryingSqlServerCache.CreateRetryProvider(OneRetry);
        retry.Retrying += (_, _) => cancellation.Cancel();
        var store = new CountingStore(StoreOverAMissingDatabase());
        var cache = new RetryingSqlServerCache(store, retry, new FakeLogger<RetryingSqlServerCache>());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetAsync(NewKey(), cancellation.Token));

        Assert.Equal(1, store.Reads);
    }

    private static SqlServerCache StoreOverAMissingDatabase() =>
        new(new SqlServerCacheOptions
        {
            ConnectionString = new SqlConnectionStringBuilder(SqlTestDatabase.Current.ConnectionString) { InitialCatalog = $"ErpAiProMissing_{Guid.CreateVersion7():N}" }.ConnectionString,
            SchemaName = CachingDbContext.SchemaName,
            TableName = CachingDbContext.DistributedCacheTable,
        });

    private static string NewKey() => $"retrying-cache-test:{Guid.CreateVersion7():N}";

    private sealed class CountingStore(IDistributedCache store) : IDistributedCache
    {
        private int _reads;

        public int Reads => Volatile.Read(ref _reads);

        public byte[]? Get(string key)
        {
            Interlocked.Increment(ref _reads);
            return store.Get(key);
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            Interlocked.Increment(ref _reads);
            return store.GetAsync(key, token);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => store.Set(key, value, options);

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            store.SetAsync(key, value, options, token);

        public void Refresh(string key) => store.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) => store.RefreshAsync(key, token);

        public void Remove(string key) => store.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default) => store.RemoveAsync(key, token);
    }
}
