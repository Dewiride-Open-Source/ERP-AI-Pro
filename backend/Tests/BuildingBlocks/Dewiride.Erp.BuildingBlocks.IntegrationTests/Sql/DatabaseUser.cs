using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Sql;

// A user without a login, impersonated on a connection that is never pooled, so the impersonation ends with the connection and
// cannot reach a connection another test takes from the pool.
internal sealed class DatabaseUser : IAsyncDisposable
{
    private readonly string _connectionString;

    private readonly string _name;

    private DatabaseUser(string connectionString, string name, SqlConnection connection)
    {
        _connectionString = connectionString;
        _name = name;
        Connection = connection;
    }

    public SqlConnection Connection { get; }

    public static async Task<DatabaseUser> CreateAsync(string connectionString, IReadOnlyCollection<string> roles, IReadOnlyCollection<string>? denials = null)
    {
        var name = $"erp_probe_{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}";
        await using (var administrator = new SqlConnection(connectionString))
        {
            await administrator.OpenAsync(TestContext.Current.CancellationToken);
            await ExecuteAsync(
                administrator,
                $"CREATE USER [{name}] WITHOUT LOGIN;{string.Concat(roles.Select(role => $"ALTER ROLE [{role}] ADD MEMBER [{name}];"))}{string.Concat((denials ?? []).Select(denial => $"DENY {denial} TO [{name}];"))}");
        }

        var connection = new SqlConnection(new SqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAsync(connection, $"EXECUTE AS USER = N'{name}';");

        return new DatabaseUser(connectionString, name, connection);
    }

    public async Task<int?> RefusalOfAsync(string statement, Guid id)
    {
        await using var transaction = (SqlTransaction)await Connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        try
        {
            await using var command = new SqlCommand(statement, Connection, transaction);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

            return null;
        }
        catch (SqlException refusal)
        {
            return refusal.Number;
        }
        finally
        {
            if (transaction.Connection is not null)
            {
                await transaction.RollbackAsync(TestContext.Current.CancellationToken);
            }
        }
    }

    public async Task<TableRights> RightsOnAsync(string table)
    {
        await using var command = new SqlCommand(
            "SELECT HAS_PERMS_BY_NAME(@table, N'OBJECT', N'INSERT'), HAS_PERMS_BY_NAME(@table, N'OBJECT', N'UPDATE'), HAS_PERMS_BY_NAME(@table, N'OBJECT', N'DELETE'), HAS_PERMS_BY_NAME(@table, N'OBJECT', N'ALTER')",
            Connection);
        command.Parameters.AddWithValue("@table", table);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        await reader.ReadAsync(TestContext.Current.CancellationToken);

        return new TableRights(reader.GetInt32(0) == 1, reader.GetInt32(1) == 1, reader.GetInt32(2) == 1, reader.GetInt32(3) == 1);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await ExecuteAsync(Connection, "REVERT;");
        }
        finally
        {
            await Connection.DisposeAsync();
            await using var administrator = new SqlConnection(_connectionString);
            await administrator.OpenAsync(TestContext.Current.CancellationToken);
            await ExecuteAsync(administrator, $"DROP USER IF EXISTS [{_name}];");
        }
    }

    private static async Task ExecuteAsync(SqlConnection connection, string statement)
    {
        await using var command = new SqlCommand(statement, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
