using Dewiride.Erp.BuildingBlocks.Auditing.Persistence;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample.Notes;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Sql;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
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
        Assert.Equal(new TableRights(Insert: true, Update: false, Delete: false, Alter: false), await member.RightsOnAsync($"audit.{table}"));
    }

    [Fact]
    public async Task AppendOnlyRole_MemberThatMayAlsoChangeTheDatabasesSchema_IsStillRefusedTruncationAndAlteration()
    {
        await using var member = await DatabaseUser.CreateAsync(database.ConnectionString, [.. ApiRoles, "db_ddladmin"]);

        Assert.Equal(NotFoundOrNoPermission, await member.RefusalOfAsync("TRUNCATE TABLE [audit].[ChangeRecords]", Guid.Empty));
        Assert.NotNull(await member.RefusalOfAsync("ALTER TABLE [audit].[ChangeRecords] ADD [Probe] int NULL", Guid.Empty));
        Assert.Equal(new TableRights(Insert: true, Update: false, Delete: false, Alter: false), await member.RightsOnAsync("audit.ChangeRecords"));
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
}
