using Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.People.Application;
using Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.ApplyAccountStates;
using Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.ListActiveAccounts;
using Dewiride.Erp.Modules.Identity.Users.People.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Dewiride.Erp.Modules.Identity.Users.UnitTests.People.Hosting;

public sealed class DirectoryAccountCheckJobTests
{
    private static readonly JobContext FirstAttempt = new(JobRunId.Create(), 1);

    [Fact]
    public async Task RunAsync_CheckDeactivatesPeople_SucceedsAndLogsOnlyTheCounts()
    {
        var logger = new FakeLogger<DirectoryAccountCheckJob>();
        var deactivated = new List<DeactivatedPersonDetails>
        {
            new(Guid.CreateVersion7(), "Asha Rao", PersonDeactivationReason.AccountDisabled),
            new(Guid.CreateVersion7(), "Kiran Shah", PersonDeactivationReason.AccountDeleted),
        };
        var job = Job(new AccountCheckDetails(3, deactivated), new AnsweringDirectory(), logger);

        var result = await job.RunAsync(FirstAttempt, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal("The nightly account check read 3 accounts and deactivated 2 people", record.Message);
        Assert.Equal(["CheckedCount", "DeactivatedCount", "{OriginalFormat}"], record.StructuredState!.Select(pair => pair.Key).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task RunAsync_DirectoryCannotAnswer_ReturnsItsErrorAndLogsNothing()
    {
        var logger = new FakeLogger<DirectoryAccountCheckJob>();
        var job = Job(new AccountCheckDetails(0, []), new AnsweringDirectory(DirectoryErrors.Unavailable), logger);

        var result = await job.RunAsync(FirstAttempt, TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.Unavailable, result.Error);
        Assert.Empty(logger.Collector.GetSnapshot());
    }

    private static DirectoryAccountCheckJob Job(AccountCheckDetails applied, IDirectoryAccounts directory, FakeLogger<DirectoryAccountCheckJob> logger) =>
        new(new DirectoryAccountCheck(new ActiveAccounts(), new AppliedStates(applied)), directory, logger);

    private sealed class ActiveAccounts : IQueryHandler<ListActiveAccountsQuery, IReadOnlyList<Guid>>
    {
        public Task<Result<IReadOnlyList<Guid>>> HandleAsync(ListActiveAccountsQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<IReadOnlyList<Guid>>([Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()]));
    }

    private sealed class AppliedStates(AccountCheckDetails applied) : ICommandHandler<ApplyAccountStatesCommand, AccountCheckDetails>
    {
        public Task<Result<AccountCheckDetails>> HandleAsync(ApplyAccountStatesCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(applied));
    }

    private sealed class AnsweringDirectory(Error? refusal = null) : IDirectoryAccounts
    {
        public Task<Result<IReadOnlyDictionary<Guid, DirectoryAccountState>>> StatesOfAsync(IReadOnlyCollection<Guid> objectIds, CancellationToken cancellationToken) =>
            Task.FromResult(refusal is null
                ? Result.Success<IReadOnlyDictionary<Guid, DirectoryAccountState>>(objectIds.ToDictionary(objectId => objectId, _ => DirectoryAccountState.Enabled))
                : Result.Fail<IReadOnlyDictionary<Guid, DirectoryAccountState>>(refusal));
    }
}
