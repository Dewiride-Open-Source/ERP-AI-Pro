using Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.ApplyAccountStates;
using Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.ListActiveAccounts;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application;

// The directory is read between the query and the command, outside any transaction, so the organisation lock is held only
// while the records change. Whoever runs the check hands it the directory: only the API composes the sign-in services that read
// it, and the hosts without them compose this module too.
internal sealed class DirectoryAccountCheck(
    IQueryHandler<ListActiveAccountsQuery, IReadOnlyList<Guid>> accounts,
    ICommandHandler<ApplyAccountStatesCommand, AccountCheckDetails> apply)
{
    public async Task<Result<AccountCheckDetails>> RunAsync(IDirectoryAccounts directory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);

        var active = await accounts.HandleAsync(new ListActiveAccountsQuery(), cancellationToken).ConfigureAwait(false);
        if (active.IsFailure)
        {
            return active.Error!;
        }

        if (active.Value.Count == 0)
        {
            return new AccountCheckDetails(0, []);
        }

        var states = await directory.StatesOfAsync(active.Value, cancellationToken).ConfigureAwait(false);
        if (states.IsFailure)
        {
            return states.Error!;
        }

        return await apply.HandleAsync(new ApplyAccountStatesCommand(states.Value), cancellationToken).ConfigureAwait(false);
    }
}
