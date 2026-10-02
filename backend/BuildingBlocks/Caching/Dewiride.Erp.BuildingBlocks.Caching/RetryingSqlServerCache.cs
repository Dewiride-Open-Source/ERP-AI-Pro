using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.Caching;

// SqlServerCache retries nothing, while every EF Core context retries transient SQL errors with the database settings. Each
// operation is one statement that may run again without harm, so the whole operation is repeated through SqlClient's own
// retry logic and its baseline transient errors. Retries that run out surface as SqlClient's AggregateException, which holds
// the failure of every attempt.
internal sealed partial class RetryingSqlServerCache : IDistributedCache
{
    private static readonly TimeSpan RetryDeltaTime = TimeSpan.FromSeconds(1);

    private readonly IDistributedCache _store;

    private readonly SqlRetryLogicBaseProvider _retry;

    private readonly ILogger<RetryingSqlServerCache> _logger;

    public RetryingSqlServerCache(IDistributedCache store, SqlRetryLogicBaseProvider retry, ILogger<RetryingSqlServerCache> logger)
    {
        _store = store;
        _retry = retry;
        _logger = logger;
        _retry.Retrying += LogRetry;
    }

    public static SqlRetryLogicBaseProvider CreateRetryProvider(DatabaseOptions database) =>
        SqlConfigurableRetryFactory.CreateExponentialRetryProvider(new SqlRetryLogicOption
        {
            NumberOfTries = database.MaxRetryCount + 1,
            DeltaTime = RetryDeltaTime,
            MaxTimeInterval = database.MaxRetryDelay,
        });

    public byte[]? Get(string key) => _retry.Execute(this, () => _store.Get(key));

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
        _retry.ExecuteAsync(this, () => _store.GetAsync(key, token), token);

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => Run(() => _store.Set(key, value, options));

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
        _retry.ExecuteAsync(this, () => _store.SetAsync(key, value, options, token), token);

    public void Refresh(string key) => Run(() => _store.Refresh(key));

    public Task RefreshAsync(string key, CancellationToken token = default) =>
        _retry.ExecuteAsync(this, () => _store.RefreshAsync(key, token), token);

    public void Remove(string key) => Run(() => _store.Remove(key));

    public Task RemoveAsync(string key, CancellationToken token = default) =>
        _retry.ExecuteAsync(this, () => _store.RemoveAsync(key, token), token);

    private void Run(Action operation) =>
        _retry.Execute(this, () =>
        {
            operation();
            return true;
        });

    private void LogRetry(object? sender, SqlRetryingEventArgs retrying) =>
        LogRetrying(_logger, retrying.Exceptions[^1], retrying.RetryCount, retrying.Delay);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A SQL Server cache command failed with a transient error and runs again in {Delay}, retry {RetryCount}")]
    private static partial void LogRetrying(ILogger logger, Exception exception, int retryCount, TimeSpan delay);
}
