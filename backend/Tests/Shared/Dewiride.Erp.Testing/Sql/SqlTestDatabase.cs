using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Dewiride.Erp.Testing.Sql;

public sealed class SqlTestDatabase : IAsyncLifetime
{
    public const string ConnectionVariable = "ERP_TEST_SQL_CONNECTION";

    public const string NamePrefix = "ErpAiProTest_";

    private const int LeftoverAgeHours = 24;

    private const string CreateStatement = "DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@name) + N';'; EXEC sp_executesql @sql;";

    private const string DropStatement =
        "IF DB_ID(@name) IS NOT NULL BEGIN DECLARE @sql nvarchar(max) = N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@name) + N';'; EXEC sp_executesql @sql; END";

    private static SqlTestDatabase? _current;

    private string? _serverConnectionString;

    public static SqlTestDatabase Current =>
        _current ?? throw new InvalidOperationException(
            "No test database exists. Declare [assembly: AssemblyFixture(typeof(SqlTestDatabase))] in the test project (docs/guides/testing.md) or pass a connection string with ErpApiFactory.WithConfiguration.");

    public static bool Exists => _current is not null;

    public string Name { get; private set; } = string.Empty;

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(server))
        {
            throw new InvalidOperationException(
                $"{ConnectionVariable} is not set. Point it at a SQL Server whose login may create databases, for example " +
                "Server=localhost;Integrated Security=True;Encrypt=True;TrustServerCertificate=True (docs/guides/testing.md).");
        }

        var builder = new SqlConnectionStringBuilder(server);
        if (!string.IsNullOrEmpty(builder.InitialCatalog))
        {
            throw new InvalidOperationException(
                $"{ConnectionVariable} must not name a database ('{builder.InitialCatalog}' was given); the fixture creates one per test process.");
        }

        _serverConnectionString = builder.ConnectionString;
        Name = $"{NamePrefix}{TimeProvider.System.GetUtcNow():yyyyMMddHHmmss}_{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}";

        await using (var connection = new SqlConnection(_serverConnectionString))
        {
            await connection.OpenAsync();
            await DropLeftoversAsync(connection);
            await ExecuteAsync(connection, CreateStatement, Name);
        }

        builder.InitialCatalog = Name;
        ConnectionString = builder.ConnectionString;
        await TestDatabaseHost.MigrateAsync(ConnectionString);
        _current = this;
    }

    public async ValueTask DisposeAsync()
    {
        if (_serverConnectionString is null)
        {
            return;
        }

        _current = null;
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(_serverConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, DropStatement, Name);
    }

    private static async Task DropLeftoversAsync(SqlConnection connection)
    {
        var leftovers = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sys.databases WHERE name LIKE @pattern AND create_date < DATEADD(hour, -@hours, GETDATE());";
            command.Parameters.AddWithValue("@pattern", NamePrefix.Replace("_", "[_]", StringComparison.Ordinal) + "%");
            command.Parameters.AddWithValue("@hours", LeftoverAgeHours);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                leftovers.Add(reader.GetString(0));
            }
        }

        foreach (var leftover in leftovers)
        {
            await ExecuteAsync(connection, DropStatement, leftover);
        }
    }

    private static async Task ExecuteAsync(SqlConnection connection, string statement, string database)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        command.Parameters.Add("@name", System.Data.SqlDbType.NVarChar, 128).Value = database;
        await command.ExecuteNonQueryAsync();
    }
}
