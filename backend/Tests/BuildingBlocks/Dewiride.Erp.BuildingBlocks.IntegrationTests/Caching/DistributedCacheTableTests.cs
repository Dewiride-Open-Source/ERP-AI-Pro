using Dewiride.Erp.BuildingBlocks.Caching.Persistence;
using Dewiride.Erp.Testing.Sql;
using Microsoft.Data.SqlClient;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Caching;

// The columns, collation and indexes the dotnet-sql-cache tool creates, which SqlServerCache's own SQL relies on.
public sealed class DistributedCacheTableTests
{
    private const string Table = $"[{CachingDbContext.SchemaName}].[{CachingDbContext.DistributedCacheTable}]";

    [Fact]
    public async Task Columns_AfterMigration_MatchTheTableTheSqlCacheToolCreates()
    {
        var columns = await QueryAsync(
            $"""
            SELECT c.name, t.name, c.max_length, c.scale, c.is_nullable, c.collation_name
            FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID(N'{Table}')
            ORDER BY c.column_id
            """,
            reader => $"{reader.GetString(0)} {reader.GetString(1)}({reader.GetInt16(2)},{reader.GetByte(3)}) {(reader.GetBoolean(4) ? "NULL" : "NOT NULL")} {(reader.IsDBNull(5) ? "-" : reader.GetString(5))}");

        Assert.Equal(
            [
                "Id nvarchar(898,0) NOT NULL SQL_Latin1_General_CP1_CS_AS",
                "Value varbinary(-1,0) NOT NULL -",
                "ExpiresAtTime datetimeoffset(10,7) NOT NULL -",
                "SlidingExpirationInSeconds bigint(8,0) NULL -",
                "AbsoluteExpiration datetimeoffset(10,7) NULL -",
            ],
            columns);
    }

    [Fact]
    public async Task Indexes_AfterMigration_AreTheClusteredPrimaryKeyOnIdAndTheExpiryIndex()
    {
        var indexes = await QueryAsync(
            $"""
            SELECT i.name, i.type_desc, i.is_primary_key, i.is_unique, COL_NAME(ic.object_id, ic.column_id)
            FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            WHERE i.object_id = OBJECT_ID(N'{Table}')
            ORDER BY i.index_id, ic.key_ordinal
            """,
            reader => $"{reader.GetString(0)} {reader.GetString(1)} primary:{reader.GetBoolean(2)} unique:{reader.GetBoolean(3)} on {reader.GetString(4)}");

        Assert.Equal(
            [
                "PK_DistributedCacheEntries CLUSTERED primary:True unique:True on Id",
                $"{DistributedCacheEntryConfiguration.ExpiresAtTimeIndex} NONCLUSTERED primary:False unique:False on ExpiresAtTime",
            ],
            indexes);
    }

    private static async Task<List<string>> QueryAsync(string sql, Func<SqlDataReader, string> describe)
    {
        await using var connection = new SqlConnection(SqlTestDatabase.Current.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var rows = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add(describe(reader));
        }

        return rows;
    }
}
