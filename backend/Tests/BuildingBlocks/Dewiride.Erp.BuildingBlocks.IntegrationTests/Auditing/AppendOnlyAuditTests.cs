using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Auditing.Persistence;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample.Notes;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Auditing;

// A database user without a login stands in for the API's: a member of the roles that read and write every table and of the
// append-only role, impersonated on a connection of its own. Every statement it may not run is tried inside a transaction that
// is rolled back, so a right it wrongly held could not harm the rows other tests read.
public sealed class AppendOnlyAuditTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    private const int PermissionDenied = 229;

    private const int NotFoundOrNoPermission = 1088;

    private static readonly string[] ApiRoles = ["db_datareader", "db_datawriter", AuditingDbContext.AppendOnlyRole];

    [Theory]
    [InlineData("ChangeRecords", "ValueCount")]
    [InlineData("SecurityEvents", "Detail")]
    public async Task AppendOnlyRole_Member_InsertsButIsRefusedEveryUpdateDeletionAndTruncation(string table, string column)
    {
        await using var member = await DatabaseUser.CreateAsync(database.ConnectionString, ApiRoles);
        var id = Guid.CreateVersion7();

        Assert.Null(await member.RefusalOfAsync(InsertInto(table), id));
        Assert.Equal(PermissionDenied, await member.RefusalOfAsync($"UPDATE [audit].[{table}] SET [{column}] = [{column}] WHERE [Id] = @id", id));
        Assert.Equal(PermissionDenied, await member.RefusalOfAsync($"DELETE FROM [audit].[{table}] WHERE [Id] = @id", id));
        Assert.Equal(NotFoundOrNoPermission, await member.RefusalOfAsync($"TRUNCATE TABLE [audit].[{table}]", id));
        Assert.Equal(new Rights(Insert: true, Update: false, Delete: false, Alter: false), await member.RightsOnAsync($"audit.{table}"));
    }

    [Fact]
    public async Task AppendOnlyRole_MemberThatMayAlsoChangeTheDatabasesSchema_IsStillRefusedTruncationAndAlteration()
    {
        await using var member = await DatabaseUser.CreateAsync(database.ConnectionString, [.. ApiRoles, "db_ddladmin"]);

        Assert.Equal(NotFoundOrNoPermission, await member.RefusalOfAsync("TRUNCATE TABLE [audit].[ChangeRecords]", Guid.Empty));
        Assert.NotNull(await member.RefusalOfAsync("ALTER TABLE [audit].[ChangeRecords] ADD [Probe] int NULL", Guid.Empty));
        Assert.Equal(new Rights(Insert: true, Update: false, Delete: false, Alter: false), await member.RightsOnAsync("audit.ChangeRecords"));
        Assert.True((await member.RightsOnAsync("test_sample.Samples")).Alter);
    }

    [Fact]
    public async Task AppendOnlyRole_Member_SavesAChangeWithItsRecordsAndRecordsASecurityEvent()
    {
        await using var member = await DatabaseUser.CreateAsync(database.ConnectionString, ApiRoles);
        var sampleId = SampleId.Create();
        var correlationId = Guid.CreateVersion7().ToString("N");

        await using (var scope = database.CreateScope())
        {
            var samples = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            samples.Database.SetDbConnection(member.Connection, contextOwnsConnection: false);
            samples.Samples.Add(new SampleAggregate(sampleId, "Saved by the API's user", new Money(1m, Currency.Inr), new SampleAddress("1", "Pune"), database.Clock.GetUtcNow()));
            samples.Notes.Add(new SampleNote(SampleNoteId.Create(), sampleId, "Saved with it", null, []));
            await samples.SaveChangesAsync(TestContext.Current.CancellationToken);

            var auditing = scope.ServiceProvider.GetRequiredService<AuditingDbContext>();
            auditing.Database.SetDbConnection(member.Connection, contextOwnsConnection: false);
            await new SqlSecurityEventRecorder(auditing, new FakeTimeProvider(TimeProvider.System.GetUtcNow()))
                .RecordAsync(new SecurityEventEntry(SecurityEventKind.SignedIn, CorrelationId: correlationId), TestContext.Current.CancellationToken);
        }

        Assert.Equal([ChangeOperation.Created], (await database.ChangeRecordsOfAsync(sampleId.Value)).Select(record => record.Operation));
        await using var check = database.CreateScope();
        Assert.True(await check.ServiceProvider.GetRequiredService<AuditingDbContext>().SecurityEvents.AnyAsync(e => e.CorrelationId == correlationId, TestContext.Current.CancellationToken));
    }

    private static string InsertInto(string table) => table == "ChangeRecords"
        ? "INSERT INTO [audit].[ChangeRecords] ([Id], [OccurredAt], [Operation], [EntityType], [EntityId], [SubjectType], [SubjectId], [ActorId], [ValueCount]) VALUES (@id, SYSUTCDATETIME(), 1, 'test.sample.samples', @id, 'test.sample.samples', @id, @id, 0)"
        : "INSERT INTO [audit].[SecurityEvents] ([Id], [OccurredAt], [Kind]) VALUES (@id, SYSUTCDATETIME(), 1)";

    private sealed record Rights(bool Insert, bool Update, bool Delete, bool Alter);

    private sealed class DatabaseUser : IAsyncDisposable
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

        public static async Task<DatabaseUser> CreateAsync(string connectionString, IReadOnlyCollection<string> roles)
        {
            var name = $"erp_append_only_probe_{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}";
            await using (var administrator = new SqlConnection(connectionString))
            {
                await administrator.OpenAsync(TestContext.Current.CancellationToken);
                await ExecuteAsync(administrator, $"CREATE USER [{name}] WITHOUT LOGIN;{string.Concat(roles.Select(role => $"ALTER ROLE [{role}] ADD MEMBER [{name}];"))}");
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

        public async Task<Rights> RightsOnAsync(string table)
        {
            await using var command = new SqlCommand(
                "SELECT HAS_PERMS_BY_NAME(@table, N'OBJECT', N'INSERT'), HAS_PERMS_BY_NAME(@table, N'OBJECT', N'UPDATE'), HAS_PERMS_BY_NAME(@table, N'OBJECT', N'DELETE'), HAS_PERMS_BY_NAME(@table, N'OBJECT', N'ALTER')",
                Connection);
            command.Parameters.AddWithValue("@table", table);
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            await reader.ReadAsync(TestContext.Current.CancellationToken);

            return new Rights(reader.GetInt32(0) == 1, reader.GetInt32(1) == 1, reader.GetInt32(2) == 1, reader.GetInt32(3) == 1);
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
}
