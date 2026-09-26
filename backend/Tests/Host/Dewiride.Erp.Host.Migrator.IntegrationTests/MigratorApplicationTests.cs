using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;
using Dewiride.Erp.Host.Migrator.IntegrationTests.Seeding;
using Dewiride.Erp.Testing.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.Host.Migrator.IntegrationTests;

public sealed class MigratorApplicationTests
{
    private const string UnreachableServer = "Server=127.0.0.1,1;Database=ErpAiPro;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=1";

    private static readonly string[] Schemas = ["platform_idempotency", "platform_system_info"];

    [Fact]
    public async Task RunAsync_MigrateOnAFreshDatabase_AppliesEveryCatalogueContext()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();

        var exitCode = await RunAsync([MigratorApplication.MigrateCommand], database.ConnectionString);

        Assert.Equal(MigratorExitCodes.Succeeded, exitCode);
        foreach (var schema in Schemas)
        {
            Assert.True(await CountAppliedAsync(database.ConnectionString, schema) > 0, $"No migration was applied to {schema}.");
        }

        Assert.Equal(MigratorExitCodes.Succeeded, await RunAsync([MigratorApplication.StatusCommand], database.ConnectionString));
    }

    [Fact]
    public async Task RunAsync_MigrateTwice_ChangesNothingTheSecondTime()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();
        Assert.Equal(MigratorExitCodes.Succeeded, await RunAsync([MigratorApplication.MigrateCommand], database.ConnectionString));
        var applied = await CountAllAppliedAsync(database.ConnectionString);

        var exitCode = await RunAsync([MigratorApplication.MigrateCommand], database.ConnectionString);

        Assert.Equal(MigratorExitCodes.Succeeded, exitCode);
        Assert.Equal(applied, await CountAllAppliedAsync(database.ConnectionString));
    }

    [Fact]
    public async Task RunAsync_StatusBeforeMigrating_ReportsPendingMigrations()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();

        var exitCode = await RunAsync([MigratorApplication.StatusCommand], database.ConnectionString);

        Assert.Equal(MigratorExitCodes.MigrationsPending, exitCode);
    }

    [Fact]
    public async Task RunAsync_MigratorConnectionString_IsUsedInsteadOfTheApplicationConnectionString()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();

        var exitCode = await RunAsync(
            [MigratorApplication.MigrateCommand],
            UnreachableServer,
            new Dictionary<string, string?> { [$"{MigratorDatabaseOptions.SectionName}:{nameof(MigratorDatabaseOptions.MigratorConnectionString)}"] = database.ConnectionString });

        Assert.Equal(MigratorExitCodes.Succeeded, exitCode);
        Assert.True(await CountAllAppliedAsync(database.ConnectionString) > 0);
    }

    [Fact]
    public async Task RunAsync_Migrate_RunsTheSeedersInOrderAfterEveryMigration()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();
        var recorder = new SeedRecorder();

        var exitCode = await RunAsync([MigratorApplication.MigrateCommand], database.ConnectionString, configureServices: services =>
        {
            services.AddSingleton(recorder);
            services.AddSeeder<SecondSeeder>(order: 20);
            services.AddSeeder<FirstSeeder>(order: 10);
        });

        Assert.Equal(MigratorExitCodes.Succeeded, exitCode);
        Assert.Equal([$"{nameof(FirstSeeder)}:0", nameof(SecondSeeder)], recorder.Runs);
    }

    [Fact]
    public async Task RunAsync_WithoutAConnectionString_ExitsWithInvalidConfiguration()
    {
        var exitCode = await RunAsync([MigratorApplication.MigrateCommand], connectionString: null);

        Assert.Equal(MigratorExitCodes.InvalidConfiguration, exitCode);
    }

    [Theory]
    [InlineData]
    [InlineData("seed")]
    [InlineData("MIGRATE")]
    public async Task RunAsync_UnknownCommand_ExitsWithUsage(params string[] args)
    {
        var exitCode = await MigratorApplication.RunAsync(args, configure: null, TestContext.Current.CancellationToken);

        Assert.Equal(MigratorExitCodes.Usage, exitCode);
    }

    private static Task<int> RunAsync(
        string[] args,
        string? connectionString,
        Dictionary<string, string?>? settings = null,
        Action<IServiceCollection>? configureServices = null) =>
        MigratorApplication.RunAsync(args, builder =>
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(settings ?? [], StringComparer.OrdinalIgnoreCase)
            {
                [ErpConfigurationSourceResolver.EndpointVariable] = string.Empty,
                [ErpConfigurationSourceResolver.SourceSetting] = ErpConfigurationSourceResolver.InMemorySource,
                [$"{DatabaseOptions.SectionName}:{nameof(DatabaseOptions.ConnectionString)}"] = connectionString,
            });
            configureServices?.Invoke(builder.Services);
        }, TestContext.Current.CancellationToken);

    private static async Task<int> CountAllAppliedAsync(string connectionString)
    {
        var total = 0;
        foreach (var schema in Schemas)
        {
            total += await CountAppliedAsync(connectionString, schema);
        }

        return total;
    }

    private static async Task<int> CountAppliedAsync(string connectionString, string schema)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand($"SELECT COUNT(*) FROM [{schema}].[__EFMigrationsHistory]", connection);

        return (int)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
