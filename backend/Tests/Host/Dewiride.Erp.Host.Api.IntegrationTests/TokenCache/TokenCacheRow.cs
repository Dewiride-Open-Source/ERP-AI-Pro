using Dewiride.Erp.BuildingBlocks.Caching.Persistence;
using Dewiride.Erp.Testing.Sql;
using Microsoft.Data.SqlClient;

namespace Dewiride.Erp.Host.Api.IntegrationTests.TokenCache;

internal sealed record TokenCacheRow(byte[] Value, DateTimeOffset ExpiresAtTime, long? SlidingExpirationInSeconds, DateTimeOffset? AbsoluteExpiration)
{
    private const string Select =
        $"SELECT [Value], [ExpiresAtTime], [SlidingExpirationInSeconds], [AbsoluteExpiration] FROM [{CachingDbContext.SchemaName}].[{CachingDbContext.DistributedCacheTable}] WHERE [Id] = @id";

    public static async Task<TokenCacheRow?> FindAsync(string id)
    {
        await using var connection = new SqlConnection(SqlTestDatabase.Current.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(Select, connection);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        if (!await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            return null;
        }

        return new TokenCacheRow(
            (byte[])reader[0],
            reader.GetDateTimeOffset(1),
            await reader.IsDBNullAsync(2, TestContext.Current.CancellationToken) ? null : reader.GetInt64(2),
            await reader.IsDBNullAsync(3, TestContext.Current.CancellationToken) ? null : reader.GetDateTimeOffset(3));
    }
}
