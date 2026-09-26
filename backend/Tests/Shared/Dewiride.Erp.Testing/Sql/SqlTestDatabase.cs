using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Dewiride.Erp.Testing.Sql;

public sealed class SqlTestDatabase : IAsyncLifetime
{
    public const string ConnectionVariable = "ERP_TEST_SQL_CONNECTION";

    public const string NamePrefix = "ErpAiProTest_";

    private const int LeftoverAgeHours = 24;

    private const string LeftoverLockResource = "ErpAiProTest_leftovers";

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

    public static string ResolveServerConnectionString(Func<string, string?> environmentVariable)
    {
        ArgumentNullException.ThrowIfNull(environmentVariable);

        var server = environmentVariable(ConnectionVariable);
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

        return builder.ConnectionString;
    }

    public async ValueTask InitializeAsync()
    {
        _serverConnectionString = ResolveServerConnectionString(Environment.GetEnvironmentVariable);
        Name = NewName();

        await using (var connection = new SqlConnection(_serverConnectionString))
        {
            await connection.OpenAsync();
            await DropLeftoversAsync(connection);
            await CreateAsync(connection, Name);
        }

        ConnectionString = new SqlConnectionStringBuilder(_serverConnectionString) { InitialCatalog = Name }.ConnectionString;
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
        await DropAsync(connection, Name);
    }

    internal static string NewName() => $"{NamePrefix}{TimeProvider.System.GetUtcNow():yyyyMMddHHmmss}_{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}";

    internal static Task CreateAsync(SqlConnection connection, string database) => ExecuteAsync(connection, CreateStatement, database);

    internal static Task DropAsync(SqlConnection connection, string database) => ExecuteAsync(connection, DropStatement, database);

    private static async Task DropLeftoversAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "sp_getapplock";
        command.CommandType = CommandType.StoredProcedure;
        command.Parameters.AddWithValue("@Resource", LeftoverLockResource);
        command.Parameters.AddWithValue("@LockMode", "Exclusive");
        command.Parameters.AddWithValue("@LockOwner", "Session");
        command.Parameters.AddWithValue("@LockTimeout", (int)TimeSpan.FromMinutes(2).TotalMilliseconds);
        var outcome = command.Parameters.Add("@Result", SqlDbType.Int);
        outcome.Direction = ParameterDirection.ReturnValue;
        await command.ExecuteNonQueryAsync();
        if ((int)outcome.Value < 0)
        {
            throw new InvalidOperationException($"The lock '{LeftoverLockResource}' guarding the cleanup of leftover test databases was not granted (sp_getapplock returned {outcome.Value}).");
        }

        foreach (var leftover in await FindLeftoversAsync(connection))
        {
            await DropAsync(connection, leftover);
        }

        await using var release = connection.CreateCommand();
        release.CommandText = "sp_releaseapplock";
        release.CommandType = CommandType.StoredProcedure;
        release.Parameters.AddWithValue("@Resource", LeftoverLockResource);
        release.Parameters.AddWithValue("@LockOwner", "Session");
        await release.ExecuteNonQueryAsync();
    }

    private static async Task<List<string>> FindLeftoversAsync(SqlConnection connection)
    {
        var leftovers = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sys.databases WHERE name LIKE @pattern AND create_date < DATEADD(hour, -@hours, GETDATE());";
        command.Parameters.AddWithValue("@pattern", NamePrefix.Replace("_", "[_]", StringComparison.Ordinal) + "%");
        command.Parameters.AddWithValue("@hours", LeftoverAgeHours);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            leftovers.Add(reader.GetString(0));
        }

        return leftovers;
    }

    private static async Task ExecuteAsync(SqlConnection connection, string statement, string database)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        command.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = database;
        await command.ExecuteNonQueryAsync();
    }
}
