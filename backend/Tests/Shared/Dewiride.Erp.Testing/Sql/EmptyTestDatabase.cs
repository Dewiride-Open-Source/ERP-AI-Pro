using Microsoft.Data.SqlClient;

namespace Dewiride.Erp.Testing.Sql;

// For tests of the migration path itself, which need a database no migration has touched; it carries the test-database
// name prefix so the leftover cleanup removes it if a test process dies.
public sealed class EmptyTestDatabase : IAsyncDisposable
{
    private readonly string _serverConnectionString;

    private EmptyTestDatabase(string serverConnectionString, string name)
    {
        _serverConnectionString = serverConnectionString;
        Name = name;
        ConnectionString = new SqlConnectionStringBuilder(serverConnectionString) { InitialCatalog = name }.ConnectionString;
    }

    public string Name { get; }

    public string ConnectionString { get; }

    public static async Task<EmptyTestDatabase> CreateAsync()
    {
        var server = SqlTestDatabase.ResolveServerConnectionString(Environment.GetEnvironmentVariable);
        var database = new EmptyTestDatabase(server, SqlTestDatabase.NewName());
        await using var connection = new SqlConnection(server);
        await connection.OpenAsync();
        await SqlTestDatabase.CreateAsync(connection, database.Name);

        return database;
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(_serverConnectionString);
        await connection.OpenAsync();
        await SqlTestDatabase.DropAsync(connection, Name);
    }
}
