using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.ApplyAccountStates;

// A person whose Entra account is disabled or deleted is deactivated at once, so their sessions end at their next request,
// with the reason their change records keep. Nobody else's position changes: their team keeps naming them, and each member's
// record answers that their manager is not active. An answer without a single enabled account cannot be the company
// directory, whose people sign in with enabled accounts, and neither can one that does not answer the account of the person
// running the check as enabled, who signed in with it; either changes nothing and counts as a directory that cannot answer, so
// a wrong answer never deactivates the administrator who could reactivate the others.
internal sealed partial class ApplyAccountStatesHandler(UsersDbContext context, IActorContext actor, ILogger<ApplyAccountStatesHandler> logger)
    : ICommandHandler<ApplyAccountStatesCommand, AccountCheckDetails>
{
    public async Task<Result<AccountCheckDetails>> HandleAsync(ApplyAccountStatesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var states = command.States;
        if (states.Count > 0 && !states.Values.Contains(DirectoryAccountState.Enabled))
        {
            LogNoEnabledAccount(logger, states.Count);
            return DirectoryErrors.Unavailable;
        }

        if (states.TryGetValue(actor.ActorId, out var own) && own != DirectoryAccountState.Enabled)
        {
            LogOwnAccountNotEnabled(logger, states.Count);
            return DirectoryErrors.Unavailable;
        }

        if (!await OrganisationLock.TryAcquireAsync(context, cancellationToken).ConfigureAwait(false))
        {
            return OrganisationLock.Busy;
        }

        if (await ActingPerson.IsInactiveAsync(context, actor, cancellationToken).ConfigureAwait(false))
        {
            return UserErrors.ActorInactive;
        }

        List<Guid?> closed = [.. states.Where(state => state.Value != DirectoryAccountState.Enabled).Select(state => (Guid?)state.Key)];
        if (closed.Count == 0)
        {
            return new AccountCheckDetails(states.Count, []);
        }

        var users = await context.Users
            .Where(u => closed.Contains(u.EntraObjectId) && u.Status == UserStatus.Active)
            .OrderBy(u => u.DisplayName)
            .ThenBy(u => u.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var deactivated = new List<DeactivatedPersonDetails>(users.Count);
        foreach (var user in users)
        {
            var reason = states[user.EntraObjectId!.Value] == DirectoryAccountState.Disabled ? DeactivationReason.AccountDisabled : DeactivationReason.AccountDeleted;
            user.Deactivate(reason);
            deactivated.Add(new DeactivatedPersonDetails(user.Id.Value, user.DisplayName, DeactivationReasons.ToPersonDeactivationReason(reason)));
        }

        return new AccountCheckDetails(states.Count, deactivated);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The company directory answered the account check of {CheckedCount} accounts without one enabled account, so nobody was deactivated")]
    private static partial void LogNoEnabledAccount(ILogger logger, int checkedCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The company directory answered the account check of {CheckedCount} accounts without the enabled account of the person running it, so nobody was deactivated")]
    private static partial void LogOwnAccountNotEnabled(ILogger logger, int checkedCount);
}
