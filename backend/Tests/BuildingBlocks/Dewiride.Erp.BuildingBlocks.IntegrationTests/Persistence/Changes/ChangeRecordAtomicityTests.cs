using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Fakes;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample.Notes;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.UnitOfWork;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Sql;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Changes;

public sealed class ChangeRecordAtomicityTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    private const string ChangeRecordsTable = "[audit].[ChangeRecords]";

    private const string SamplesTable = "[test_sample].[Samples]";

    private const int PermissionDenied = 229;

    [Fact]
    public async Task SaveChangesAsync_RefusedForConcurrency_WritesNoRecordAndTheSaveAfterAReloadWritesOne()
    {
        var id = await AddSampleAsync("Contended");
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var sample = await context.Samples.SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
        database.Clock.Advance(TimeSpan.FromMinutes(1));
        await RenameElsewhereAsync(id, "Winner");
        sample.Rename("Loser", database.Clock.GetUtcNow());
        sample.ClearDomainEvents();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Empty(context.ChangeTracker.Entries<ChangeRecord>());
        Assert.Equal([ChangeOperation.Created, ChangeOperation.Updated], (await database.ChangeRecordsOfAsync(id.Value)).Select(record => record.Operation));

        await context.Entry(sample).ReloadAsync(TestContext.Current.CancellationToken);
        database.Clock.Advance(TimeSpan.FromMinutes(1));
        sample.Rename("Second try", database.Clock.GetUtcNow());
        sample.ClearDomainEvents();
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var records = await database.ChangeRecordsOfAsync(id.Value);
        Assert.Equal([ChangeOperation.Created, ChangeOperation.Updated, ChangeOperation.Updated], records.Select(record => record.Operation));
        Assert.Contains("Second try", records[2].Changes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveChangesAsync_InsideATransactionRolledBack_LeavesNoRecord()
    {
        var id = await AddSampleAsync("Rolled back");
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        await context.Database.CreateExecutionStrategy().ExecuteAsync(
            async token =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync(token);
                var sample = await context.Samples.SingleAsync(s => s.Id == id, token);
                sample.Reprice(new Money(99m, Currency.Inr));
                await context.SaveChangesAsync(token);
                await transaction.RollbackAsync(token);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal([ChangeOperation.Created], (await database.ChangeRecordsOfAsync(id.Value)).Select(record => record.Operation));
    }

    [Theory]
    [InlineData(RenameOutcome.FailAfterRenaming)]
    [InlineData(RenameOutcome.ThrowAfterRenaming)]
    public async Task HandleAsync_HandlerThatFailsAfterTheChange_LeavesNoRecord(RenameOutcome outcome)
    {
        var id = await AddSampleAsync("Stable");

        if (outcome == RenameOutcome.ThrowAfterRenaming)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => RenameAsync(new RenameSampleCommand(id, "Changed", outcome)));
        }
        else
        {
            Assert.True((await RenameAsync(new RenameSampleCommand(id, "Changed", outcome))).IsFailure);
        }

        Assert.Single(await database.ChangeRecordsOfAsync(id.Value));
    }

    [Fact]
    public async Task HandleAsync_DomainEventRoundThatThrows_RollsTheRecordsOfTheEarlierRoundBack()
    {
        var id = await AddSampleAsync("Intact");

        await Assert.ThrowsAsync<InvalidOperationException>(() => RenameAsync(new RenameSampleCommand(id, SampleRenamedHandler.PoisonName)));

        Assert.Equal([ChangeOperation.Created], (await database.ChangeRecordsOfAsync(id.Value)).Select(record => record.Operation));
    }

    [Fact]
    public async Task RunAsync_SaveThatFailsTransiently_CommitsOneSetOfRecords()
    {
        var id = await AddNoteAsync("Retried in a unit of work");
        var failure = new TransientFailureInterceptor(ChangeRecordsTable);
        await using var scope = database.CreateScope();
        await using var context = database.CreateContext(failure);
        var signal = scope.ServiceProvider.GetRequiredService<UnitOfWorkSignal>();
        database.Clock.Advance(TimeSpan.FromMinutes(1));

        var result = await EfUnitOfWork.RunAsync(
            context,
            scope.ServiceProvider.GetRequiredService<DomainEventDispatcher>(),
            signal,
            async token =>
            {
                var note = await context.Notes.SingleAsync(n => n.Id == id, token);
                note.Edit("Edited once", new DateOnly(2027, 1, 1));

                return Result.Success("done");
            },
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(signal.Committed);
        Assert.Equal(1, failure.Failures);
        Assert.Equal([ChangeOperation.Created, ChangeOperation.Updated], (await database.ChangeRecordsOfAsync(id.Value)).Select(record => record.Operation));
    }

    [Fact]
    public async Task SaveChangesAsync_PlainSaveThatFailsTransiently_CommitsOneSetOfRecords()
    {
        var failure = new TransientFailureInterceptor(ChangeRecordsTable);
        var sampleId = await AddSampleAsync("Owner of a retried note");
        var id = SampleNoteId.Create();

        await using (var context = database.CreateContext(failure))
        {
            context.Notes.Add(new SampleNote(id, sampleId, "Retried by the execution strategy", null, ["retry"]));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, failure.Failures);
        Assert.Equal([ChangeOperation.Created], (await database.ChangeRecordsOfAsync(id.Value)).Select(record => record.Operation));
    }

    [Fact]
    public async Task SaveChangesAsync_RecordTheDatabaseRefusesToInsert_SavesNeitherTheChangeNorItsRecord()
    {
        await using var writer = await DatabaseUser.CreateAsync(database.ConnectionString, ["db_datareader", "db_datawriter"], ["INSERT ON SCHEMA::[audit]"]);
        var log = new CommandLog();
        var id = SampleId.Create();

        await using (var context = database.CreateContext(log))
        {
            context.Database.SetDbConnection(writer.Connection, contextOwnsConnection: false);
            context.Samples.Add(new SampleAggregate(id, "Refused with its record", new Money(1m, Currency.Inr), new SampleAddress("1", "Pune"), database.Clock.GetUtcNow()));

            var refusal = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));

            Assert.Equal(PermissionDenied, Assert.IsType<SqlException>(refusal.InnerException).Number);
        }

        Assert.Contains(log.Commands, command => command.Contains(SamplesTable, StringComparison.Ordinal));
        await using var scope = database.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<SampleDbContext>().Samples.IncludeDeleted().AnyAsync(sample => sample.Id == id, TestContext.Current.CancellationToken));
        Assert.Empty(await database.ChangeRecordsOfAsync(id.Value));
    }

    private async Task<SampleId> AddSampleAsync(string name)
    {
        var id = SampleId.Create();
        await database.AddAsync(new SampleAggregate(id, name, new Money(1m, Currency.Inr), new SampleAddress("1", "Pune"), database.Clock.GetUtcNow()));

        return id;
    }

    private async Task<SampleNoteId> AddNoteAsync(string text)
    {
        var sampleId = await AddSampleAsync($"Owner of {text}");
        var id = SampleNoteId.Create();
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        context.Notes.Add(new SampleNote(id, sampleId, text, null, []));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
    }

    private async Task RenameElsewhereAsync(SampleId id, string name)
    {
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var sample = await context.Samples.SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
        sample.Rename(name, database.Clock.GetUtcNow());
        sample.ClearDomainEvents();
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Result<string>> RenameAsync(RenameSampleCommand command)
    {
        await using var scope = database.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<ICommandHandler<RenameSampleCommand, string>>().HandleAsync(command, TestContext.Current.CancellationToken);
    }
}
