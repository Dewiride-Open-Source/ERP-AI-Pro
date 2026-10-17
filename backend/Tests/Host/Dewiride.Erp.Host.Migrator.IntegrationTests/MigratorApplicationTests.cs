using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Auditing;
using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.BuildingBlocks.Persistence.Retention;
using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;
using Dewiride.Erp.Host.Migrator.IntegrationTests.Retention;
using Dewiride.Erp.Host.Migrator.IntegrationTests.Seeding;
using Dewiride.Erp.Testing.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.Host.Migrator.IntegrationTests;

public sealed class MigratorApplicationTests
{
    private const string UnreachableServer = "Server=127.0.0.1,1;Database=ErpAiPro;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=1";

    private const string FilesSchema = "files";

    private const string CachingSchema = "platform_caching";

    private static readonly string[] Schemas = [FilesSchema, CachingSchema, "platform_idempotency", "audit", "platform_system_info"];

    private static readonly string[] FilesTables = ["Attachments", "DownloadLinks", "DownloadRedemptions", "StoredContents", "UploadReservations"];

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
    public async Task RunAsync_MigrateWithoutAnyAttachmentSetting_SucceedsAndCreatesTheFilesTables()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();
        var attachmentSettingsPresent = true;

        var exitCode = await RunAsync(
            [MigratorApplication.MigrateCommand],
            database.ConnectionString,
            inspectConfiguration: configuration => attachmentSettingsPresent = configuration.GetSection(AttachmentsOptions.SectionName).Exists());

        Assert.False(attachmentSettingsPresent, $"The test process supplies {AttachmentsOptions.SectionName} settings, so this test cannot prove the migrator runs without them.");
        Assert.Equal(MigratorExitCodes.Succeeded, exitCode);
        Assert.Equal(FilesTables, await ListTablesAsync(database.ConnectionString, FilesSchema));
    }

    [Fact]
    public async Task RunAsync_MigrateOnAFreshDatabase_CreatesTheDistributedCacheTableTheApiCannotCreateItself()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();

        var exitCode = await RunAsync([MigratorApplication.MigrateCommand], database.ConnectionString);

        Assert.Equal(MigratorExitCodes.Succeeded, exitCode);
        Assert.Equal(["DistributedCacheEntries"], await ListTablesAsync(database.ConnectionString, CachingSchema));
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
        var recorder = new RunRecorder();

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
    public async Task RunAsync_Migrate_RunsTheRetentionSweepsAfterTheSeeders()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();
        var recorder = new RunRecorder();

        var exitCode = await RunAsync([MigratorApplication.MigrateCommand], database.ConnectionString, configureServices: services =>
        {
            services.AddSingleton(recorder);
            services.AddRetentionSweep<RecordingSweep>();
            services.AddSeeder<SecondSeeder>(order: 20);
        });

        Assert.Equal(MigratorExitCodes.Succeeded, exitCode);
        Assert.Equal([nameof(SecondSeeder), nameof(RecordingSweep)], recorder.Runs);
    }

    [Fact]
    public async Task RunAsync_MigrateWithAFailingSweep_SucceedsAndStillRunsTheOtherSweeps()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();
        var recorder = new RunRecorder();

        var exitCode = await RunAsync([MigratorApplication.MigrateCommand], database.ConnectionString, configureServices: services =>
        {
            services.AddSingleton(recorder);
            services.AddRetentionSweep<FailingSweep>();
            services.AddRetentionSweep<RecordingSweep>();
        });

        Assert.Equal(MigratorExitCodes.Succeeded, exitCode);
        Assert.Equal([nameof(FailingSweep), nameof(RecordingSweep)], recorder.Runs);
        Assert.True(await CountAllAppliedAsync(database.ConnectionString) > 0);
    }

    [Fact]
    public async Task RunAsync_RetainOnAMigratedDatabase_RunsTheRetentionSweepsButNoSeeder()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();
        Assert.Equal(MigratorExitCodes.Succeeded, await RunAsync([MigratorApplication.MigrateCommand], database.ConnectionString));
        var recorder = new RunRecorder();

        var exitCode = await RunAsync([MigratorApplication.RetainCommand], database.ConnectionString, configureServices: services =>
        {
            services.AddSingleton(recorder);
            services.AddSeeder<SecondSeeder>(order: 20);
            services.AddRetentionSweep<RecordingSweep>();
        });

        Assert.Equal(MigratorExitCodes.Succeeded, exitCode);
        Assert.Equal([nameof(RecordingSweep)], recorder.Runs);
    }

    [Fact]
    public async Task RunAsync_RetainWithAFailingSweep_RunsTheOtherSweepsAndExitsWithFailed()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();
        Assert.Equal(MigratorExitCodes.Succeeded, await RunAsync([MigratorApplication.MigrateCommand], database.ConnectionString));
        var recorder = new RunRecorder();

        var exitCode = await RunAsync([MigratorApplication.RetainCommand], database.ConnectionString, configureServices: services =>
        {
            services.AddSingleton(recorder);
            services.AddRetentionSweep<FailingSweep>();
            services.AddRetentionSweep<RecordingSweep>();
        });

        Assert.Equal(MigratorExitCodes.Failed, exitCode);
        Assert.Equal([nameof(FailingSweep), nameof(RecordingSweep)], recorder.Runs);
    }

    [Fact]
    public async Task RunAsync_RetainOnAFreshDatabase_AppliesNoMigration()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();

        var exitCode = await RunAsync([MigratorApplication.RetainCommand], database.ConnectionString);

        Assert.Equal(MigratorExitCodes.Failed, exitCode);
        Assert.Equal(0, await CountHistoryTablesAsync(database.ConnectionString));
    }

    [Fact]
    public async Task RunAsync_Retain_DeletesTheAuditRowsPastTheirRetentionAndKeepsTheRest()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();
        Assert.Equal(MigratorExitCodes.Succeeded, await RunAsync([MigratorApplication.MigrateCommand], database.ConnectionString));
        var longAgo = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var yesterday = TimeProvider.System.GetUtcNow().AddDays(-1);
        var oldEvent = await AddSecurityEventAsync(database.ConnectionString, longAgo);
        var recentEvent = await AddSecurityEventAsync(database.ConnectionString, yesterday);
        var oldRecord = await AddChangeRecordAsync(database.ConnectionString, longAgo);
        var recentRecord = await AddChangeRecordAsync(database.ConnectionString, yesterday);

        var exitCode = await RunAsync(
            [MigratorApplication.RetainCommand],
            database.ConnectionString,
            new Dictionary<string, string?> { [$"{AuditingOptions.SectionName}:{nameof(AuditingOptions.ChangeRecordRetentionYears)}"] = "8" });

        Assert.Equal(MigratorExitCodes.Succeeded, exitCode);
        bool[] kept =
        [
            await ExistsAsync(database.ConnectionString, "SecurityEvents", oldEvent),
            await ExistsAsync(database.ConnectionString, "SecurityEvents", recentEvent),
            await ExistsAsync(database.ConnectionString, "ChangeRecords", oldRecord),
            await ExistsAsync(database.ConnectionString, "ChangeRecords", recentRecord),
        ];
        Assert.Equal([false, true, false, true], kept);
    }

    [Fact]
    public async Task RunAsync_WithoutAConnectionString_ExitsWithInvalidConfiguration()
    {
        var exitCode = await RunAsync([MigratorApplication.MigrateCommand], connectionString: null);

        Assert.Equal(MigratorExitCodes.InvalidConfiguration, exitCode);
    }

    [Fact]
    public async Task RunAsync_OptionValueThatCannotBeConverted_ExitsWithInvalidConfiguration()
    {
        var exitCode = await RunAsync(
            [MigratorApplication.MigrateCommand],
            UnreachableServer,
            new Dictionary<string, string?> { [$"{DatabaseOptions.SectionName}:{nameof(DatabaseOptions.CommandTimeout)}"] = "soon" });

        Assert.Equal(MigratorExitCodes.InvalidConfiguration, exitCode);
    }

    [Fact]
    public async Task RunAsync_NoConfigurationSourceOutsideDevelopment_ExitsWithInvalidConfiguration()
    {
        var exitCode = await RunAsync([MigratorApplication.MigrateCommand], UnreachableServer, useInMemorySource: false);

        Assert.Equal(MigratorExitCodes.InvalidConfiguration, exitCode);
    }

    [Fact]
    public async Task RunAsync_UnreachableDatabase_ExitsWithFailed()
    {
        var exitCode = await RunAsync(
            [MigratorApplication.MigrateCommand],
            UnreachableServer,
            new Dictionary<string, string?> { [$"{DatabaseOptions.SectionName}:{nameof(DatabaseOptions.MaxRetryCount)}"] = "0" });

        Assert.Equal(MigratorExitCodes.Failed, exitCode);
    }

    [Fact]
    public async Task RunAsync_SeederThrows_ExitsWithFailedWithoutRunningLaterSeeders()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();
        var recorder = new RunRecorder();

        var exitCode = await RunAsync([MigratorApplication.MigrateCommand], database.ConnectionString, configureServices: services =>
        {
            services.AddSingleton(recorder);
            services.AddSeeder<ThrowingSeeder>(order: 10);
            services.AddSeeder<SecondSeeder>(order: 20);
        });

        Assert.Equal(MigratorExitCodes.Failed, exitCode);
        Assert.Equal([nameof(ThrowingSeeder)], recorder.Runs);
    }

    [Fact]
    public async Task RunAsync_CancelledBeforeMigrating_ExitsWithFailed()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var exitCode = await RunAsync([MigratorApplication.MigrateCommand], database.ConnectionString, cancellationToken: cancellation.Token);

        Assert.Equal(MigratorExitCodes.Failed, exitCode);
        Assert.Equal(0, await CountHistoryTablesAsync(database.ConnectionString));
    }

    [Theory]
    [InlineData]
    [InlineData("seed")]
    [InlineData("MIGRATE")]
    [InlineData("RETAIN")]
    public async Task RunAsync_UnknownCommand_ExitsWithUsage(params string[] args)
    {
        var exitCode = await MigratorApplication.RunAsync(args, configure: null, TestContext.Current.CancellationToken);

        Assert.Equal(MigratorExitCodes.Usage, exitCode);
    }

    private static Task<int> RunAsync(
        string[] args,
        string? connectionString,
        Dictionary<string, string?>? settings = null,
        Action<IServiceCollection>? configureServices = null,
        bool useInMemorySource = true,
        Action<IConfiguration>? inspectConfiguration = null,
        CancellationToken? cancellationToken = null) =>
        MigratorApplication.RunAsync(args, builder =>
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(settings ?? [], StringComparer.OrdinalIgnoreCase)
            {
                [ErpConfigurationSourceResolver.EndpointVariable] = string.Empty,
                [ErpConfigurationSourceResolver.SourceSetting] = useInMemorySource ? ErpConfigurationSourceResolver.InMemorySource : null,
                [$"{DatabaseOptions.SectionName}:{nameof(DatabaseOptions.ConnectionString)}"] = connectionString,
            });
            configureServices?.Invoke(builder.Services);
            inspectConfiguration?.Invoke(builder.Configuration);
        }, cancellationToken ?? TestContext.Current.CancellationToken);

    private static async Task<int> CountAllAppliedAsync(string connectionString)
    {
        var total = 0;
        foreach (var schema in Schemas)
        {
            total += await CountAppliedAsync(connectionString, schema);
        }

        return total;
    }

    private static async Task<int> CountHistoryTablesAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand("SELECT COUNT(*) FROM sys.tables WHERE name = N'__EFMigrationsHistory'", connection);

        return (int)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<string[]> ListTablesAsync(string connectionString, string schema)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(
            "SELECT t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE s.name = @schema AND t.name <> N'__EFMigrationsHistory'",
            connection);
        command.Parameters.AddWithValue("@schema", schema);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var tables = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        return [.. tables.Order(StringComparer.Ordinal)];
    }

    private static Task<Guid> AddSecurityEventAsync(string connectionString, DateTimeOffset occurredAt) =>
        InsertAsync(connectionString, "INSERT INTO [audit].[SecurityEvents] ([Id], [OccurredAt], [Kind]) VALUES (@id, @occurredAt, 2)", occurredAt);

    private static Task<Guid> AddChangeRecordAsync(string connectionString, DateTimeOffset occurredAt) =>
        InsertAsync(
            connectionString,
            "INSERT INTO [audit].[ChangeRecords] ([Id], [OccurredAt], [Operation], [EntityType], [EntityId], [SubjectType], [SubjectId], [ActorId], [ValueCount]) VALUES (@id, @occurredAt, 1, 'test.migrator.retention', @id, 'test.migrator.retention', @id, @id, 0)",
            occurredAt);

    private static async Task<Guid> InsertAsync(string connectionString, string statement, DateTimeOffset occurredAt)
    {
        var id = Guid.CreateVersion7();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(statement, connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@occurredAt", occurredAt);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        return id;
    }

    private static async Task<bool> ExistsAsync(string connectionString, string table, Guid id)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand($"SELECT COUNT(*) FROM [audit].[{table}] WHERE [Id] = @id", connection);
        command.Parameters.AddWithValue("@id", id);

        return (int)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))! == 1;
    }

    private static async Task<int> CountAppliedAsync(string connectionString, string schema)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand($"SELECT COUNT(*) FROM [{schema}].[__EFMigrationsHistory]", connection);

        return (int)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
