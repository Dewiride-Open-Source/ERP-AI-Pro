using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Migrations;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.Testing.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence;

public sealed class DatabaseMigratorTests
{
    private const string LaterMigration = "29991231235959_FromALaterRelease";

    [Fact]
    public async Task GetStatusAsync_FreshDatabase_ReportsEveryDefinedMigrationPendingAndNoneUnknown()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();
        await using var provider = BuildProvider(database.ConnectionString);

        var status = Assert.Single(await provider.GetRequiredService<DatabaseMigrator>().GetStatusAsync(TestContext.Current.CancellationToken));

        Assert.Equal(SampleDbContext.SchemaName, status.Schema);
        Assert.NotEmpty(status.Pending);
        Assert.Equal(DefinedMigrations(provider), status.Pending);
        Assert.Empty(status.Unknown);
    }

    [Fact]
    public async Task GetStatusAsync_HistoryHoldsAMigrationTheContextDoesNotDefine_ReportsItUnknownAndNothingPending()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();
        await using var provider = BuildProvider(database.ConnectionString);
        var migrator = provider.GetRequiredService<DatabaseMigrator>();
        await migrator.MigrateAllAsync(TestContext.Current.CancellationToken);
        await AddHistoryRowAsync(database.ConnectionString, LaterMigration);

        var status = Assert.Single(await migrator.GetStatusAsync(TestContext.Current.CancellationToken));

        Assert.Empty(status.Pending);
        Assert.Equal([LaterMigration], status.Unknown);
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:ConnectionString"] = connectionString,
                [ErpConfigurationSourceResolver.SourceSetting] = ErpConfigurationSourceResolver.InMemorySource,
            })
            .Build());
        services.AddLogging();
        services.AddErpPersistenceCore();
        services.AddModuleDbContext<SampleDbContext>(SampleDbContext.SchemaName);

        return services.BuildServiceProvider();
    }

    private static List<string> DefinedMigrations(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();

        return [.. scope.ServiceProvider.GetRequiredService<SampleDbContext>().Database.GetMigrations()];
    }

    private static async Task AddHistoryRowAsync(string connectionString, string migrationId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(
            $"INSERT INTO [{SampleDbContext.SchemaName}].[{ModuleDbContextRegistration.MigrationsHistoryTable}] ([MigrationId], [ProductVersion]) VALUES (@migrationId, N'10.0.13')",
            connection);
        command.Parameters.AddWithValue("@migrationId", migrationId);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
