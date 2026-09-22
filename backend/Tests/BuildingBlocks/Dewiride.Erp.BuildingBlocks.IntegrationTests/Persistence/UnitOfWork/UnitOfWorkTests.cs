using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.Application.Pipeline.Steps;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.UnitOfWork;

public sealed class UnitOfWorkTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    private static readonly Guid Erin = new("55555555-5555-5555-5555-555555555555");

    [Fact]
    public async Task HandleAsync_SuccessfulCommand_CommitsTheHandlerWriteAndTheEventHandlerWriteTogether()
    {
        var id = await AddAsync("Before");
        database.Actor.ActorId = Erin;

        var result = await RenameAsync(new RenameSampleCommand(id, "After"));

        Assert.Equal("Before", result.Value);
        var sample = await FindWithLinesAsync(id);
        Assert.Equal("After", sample.Name);
        Assert.Equal(Erin, sample.ModifiedBy);
        Assert.Equal("renamed Before to After", Assert.Single(sample.Lines).Description);
    }

    [Theory]
    [InlineData(RenameOutcome.FailAfterRenaming)]
    [InlineData(RenameOutcome.ThrowAfterRenaming)]
    public async Task HandleAsync_HandlerFailsOrThrowsAfterChangingTheAggregate_LeavesNoTrace(RenameOutcome outcome)
    {
        var id = await AddAsync("Stable");

        if (outcome == RenameOutcome.ThrowAfterRenaming)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => RenameAsync(new RenameSampleCommand(id, "Changed", outcome)));
        }
        else
        {
            Assert.Equal(RenameSampleHandler.RefusedCode, (await RenameAsync(new RenameSampleCommand(id, "Changed", outcome))).Error!.Code);
        }

        var sample = await FindWithLinesAsync(id);
        Assert.Equal("Stable", sample.Name);
        Assert.Null(sample.ModifiedAt);
        Assert.Empty(sample.Lines);
    }

    [Fact]
    public async Task HandleAsync_EventHandlerThrows_RollsBackTheHandlerWriteToo()
    {
        var id = await AddAsync("Intact");

        await Assert.ThrowsAsync<InvalidOperationException>(() => RenameAsync(new RenameSampleCommand(id, SampleRenamedHandler.PoisonName)));

        var sample = await FindWithLinesAsync(id);
        Assert.Equal("Intact", sample.Name);
        Assert.Empty(sample.Lines);
    }

    [Fact]
    public async Task HandleAsync_InvalidCommand_IsRejectedBeforeTheHandler()
    {
        var result = await RenameAsync(new RenameSampleCommand(SampleId.Create(), new string('x', SampleAggregate.NameMaxLength + 1)));

        Assert.Equal(ValidationStep.Code, result.Error!.Code);
        Assert.Contains(nameof(RenameSampleCommand.NewName), result.Error.Fields.Keys);
    }

    [Fact]
    public async Task HandleAsync_UnknownSample_ReturnsTheHandlersNotFound()
    {
        var result = await RenameAsync(new RenameSampleCommand(SampleId.Create(), "Nobody"));

        Assert.Equal(RenameSampleHandler.MissingCode, result.Error!.Code);
        Assert.Equal(ErrorKind.NotFound, result.Error.Kind);
    }

    [Fact]
    public async Task RunAsync_RowChangedBetweenReadAndSave_ReturnsAConcurrencyConflict()
    {
        var id = await AddAsync("Contended");
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<DomainEventDispatcher>();
        var signal = scope.ServiceProvider.GetRequiredService<UnitOfWorkSignal>();

        var result = await EfUnitOfWork.RunAsync(context, dispatcher, signal, async token =>
        {
            var sample = await context.Samples.SingleAsync(s => s.Id == id, token);
            await RenameElsewhereAsync(id, "Winner");
            sample.Rename("Loser", database.Clock.GetUtcNow());

            return Result.Success("done");
        }, TestContext.Current.CancellationToken);

        Assert.False(signal.Committed);
        Assert.Equal(EfUnitOfWork.ConcurrencyConflictCode, result.Error!.Code);
        Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
        Assert.Contains(nameof(SampleAggregate), result.Error.Message, StringComparison.Ordinal);
        Assert.Equal("Winner", (await database.FindAsync(id)).Name);
    }

    [Fact]
    public async Task RunAsync_InsideAnAmbientTransaction_JoinsItInsteadOfCommitting()
    {
        var id = await AddAsync("Ambient");
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<DomainEventDispatcher>();
        var signal = scope.ServiceProvider.GetRequiredService<UnitOfWorkSignal>();

        await context.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(token);
            var result = await EfUnitOfWork.RunAsync(context, dispatcher, signal, async inner =>
            {
                var sample = await context.Samples.SingleAsync(s => s.Id == id, inner);
                sample.Rename("Joined", database.Clock.GetUtcNow());

                return Result.Success("done");
            }, token);
            Assert.True(result.IsSuccess);
            Assert.Equal("Joined", await context.Samples.Where(s => s.Id == id).Select(s => s.Name).SingleAsync(token));
            await transaction.RollbackAsync(token);
        }, TestContext.Current.CancellationToken);

        Assert.Equal("Ambient", (await database.FindAsync(id)).Name);
    }

    private async Task<SampleId> AddAsync(string name)
    {
        var id = SampleId.Create();
        await database.AddAsync(new SampleAggregate(id, name, new Money(1m, Currency.Inr), new SampleAddress("1", "Pune"), database.Clock.GetUtcNow()));

        return id;
    }

    private async Task<Result<string>> RenameAsync(RenameSampleCommand command)
    {
        await using var scope = database.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<ICommandHandler<RenameSampleCommand, string>>().HandleAsync(command, TestContext.Current.CancellationToken);
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

    private async Task<SampleAggregate> FindWithLinesAsync(SampleId id)
    {
        await using var scope = database.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<SampleDbContext>().Samples.AsNoTracking().Include(s => s.Lines).SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
    }
}
