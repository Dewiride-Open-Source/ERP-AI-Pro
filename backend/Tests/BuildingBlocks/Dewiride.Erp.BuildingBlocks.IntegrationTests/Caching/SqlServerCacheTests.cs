using Dewiride.Erp.BuildingBlocks.Caching;
using Dewiride.Erp.Testing.Sql;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Caching;

// The expiry tests run a second SqlServerCache on the product's table with a fake clock and every key unique. SqlServerCache
// starts its first purge on its first operation and reads its clock only when the purge runs, so even a first operation
// before the clock moves could let a late purge delete, by the moved clock, rows of other tests that have not expired; that
// instance therefore gets a purge interval no clock reaches, and only real-clock purges ever delete rows of the shared table.
public sealed class SqlServerCacheTests : IDisposable
{
    private static readonly byte[] Value = [1, 2, 3, 4, 5];

    private readonly IHost _host = TestDatabaseHost.Build(SqlTestDatabase.Current.ConnectionString);

    private readonly FakeTimeProvider _clock = new(TimeProvider.System.GetUtcNow());

    [Fact]
    public async Task SetAsyncThenGetAsync_KeyedCache_ReturnsTheStoredValue()
    {
        var cache = KeyedCache();
        var key = UniqueKey();

        await cache.SetAsync(key, Value, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) }, TestContext.Current.CancellationToken);

        Assert.Equal(Value, await cache.GetAsync(key, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveAsync_StoredEntry_IsGoneForTheNextRead()
    {
        var cache = KeyedCache();
        var key = UniqueKey();
        await cache.SetAsync(key, Value, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) }, TestContext.Current.CancellationToken);

        await cache.RemoveAsync(key, TestContext.Current.CancellationToken);

        Assert.Null(await cache.GetAsync(key, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAsync_KeyDifferingOnlyInCase_IsAnotherEntry()
    {
        var cache = KeyedCache();
        var key = UniqueKey();
        await cache.SetAsync(key.ToUpperInvariant(), Value, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) }, TestContext.Current.CancellationToken);

        Assert.Null(await cache.GetAsync(key.ToLowerInvariant(), TestContext.Current.CancellationToken));
        Assert.Equal(Value, await cache.GetAsync(key.ToUpperInvariant(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAsync_AfterTheAbsoluteExpiration_FindsNothing()
    {
        var cache = CacheOnTheFakeClock();
        var key = UniqueKey();
        await cache.SetAsync(key, Value, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) }, TestContext.Current.CancellationToken);
        Assert.Equal(Value, await cache.GetAsync(key, TestContext.Current.CancellationToken));

        _clock.Advance(TimeSpan.FromSeconds(61));

        Assert.Null(await cache.GetAsync(key, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAsync_WithinTheSlidingWindowOfEachRead_KeepsTheEntryUntilAWindowPassesWithoutARead()
    {
        var cache = CacheOnTheFakeClock();
        var key = UniqueKey();
        await cache.SetAsync(key, Value, new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(1), AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(4) }, TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromSeconds(50));
        var slid = await cache.GetAsync(key, TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromSeconds(50));
        var slidAgain = await cache.GetAsync(key, TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromSeconds(61));
        var expired = await cache.GetAsync(key, TestContext.Current.CancellationToken);

        Assert.Equal(Value, slid);
        Assert.Equal(Value, slidAgain);
        Assert.Null(expired);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private static string UniqueKey() => $"sql-server-cache-test:{Guid.CreateVersion7():N}";

    private IDistributedCache KeyedCache() => _host.Services.GetRequiredKeyedService<IDistributedCache>(CachingRegistration.SqlServerCacheKey);

    private SqlServerCache CacheOnTheFakeClock()
    {
        var product = _host.Services.GetRequiredService<IOptionsMonitor<SqlServerCacheOptions>>().Get(CachingRegistration.SqlServerCacheKey);

        return new SqlServerCache(new SqlServerCacheOptions
        {
            ConnectionString = product.ConnectionString,
            SchemaName = product.SchemaName,
            TableName = product.TableName,
            ExpiredItemsDeletionInterval = TimeSpan.MaxValue,
            SystemClock = new FakeClock(_clock),
        });
    }

    private sealed class FakeClock(TimeProvider time) : ISystemClock
    {
        public DateTimeOffset UtcNow => time.GetUtcNow();
    }
}
