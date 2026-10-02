using Dewiride.Erp.BuildingBlocks.Caching.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Caching;

// SqlServerCache opens its own connections and sets no command timeout, so the database timeout travels in the connection
// string. Its clock stays the system clock: every operation may purge each expired row of the whole table by that clock, and
// test hosts that move a fake clock share one database per test process.
internal sealed class SqlServerCacheOptionsSetup(IOptions<DatabaseOptions> database) : IConfigureNamedOptions<SqlServerCacheOptions>
{
    public static readonly TimeSpan ExpiredItemsDeletionInterval = TimeSpan.FromMinutes(5);

    public void Configure(SqlServerCacheOptions options) => Configure(Microsoft.Extensions.Options.Options.DefaultName, options);

    public void Configure(string? name, SqlServerCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!string.Equals(name, CachingRegistration.SqlServerCacheKey, StringComparison.Ordinal))
        {
            return;
        }

        var settings = database.Value;
        if (!string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            options.ConnectionString = new SqlConnectionStringBuilder(settings.ConnectionString) { CommandTimeout = (int)settings.CommandTimeout.TotalSeconds }.ConnectionString;
        }

        options.SchemaName = CachingDbContext.SchemaName;
        options.TableName = CachingDbContext.DistributedCacheTable;
        options.ExpiredItemsDeletionInterval = ExpiredItemsDeletionInterval;
    }
}
