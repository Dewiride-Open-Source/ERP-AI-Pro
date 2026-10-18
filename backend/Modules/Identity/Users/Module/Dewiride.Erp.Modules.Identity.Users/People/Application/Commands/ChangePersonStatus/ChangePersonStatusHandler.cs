using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Shared.Versioning;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.ChangePersonStatus;

// An administrator who deactivated their own record would be signed out at once, perhaps with no other administrator left
// to reactivate it. Every change takes the organisation lock first, so the record of the person making it is read as the
// deactivations and deletions before it left it (ActingPerson), and a deactivation moves the person's team to the manager the
// request names in the same transaction, so a refusal of either part changes nothing.
internal sealed class ChangePersonStatusHandler(UsersDbContext context, IActorContext actor, TimeProvider timeProvider) : ICommandHandler<ChangePersonStatusCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(ChangePersonStatusCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!RecordVersion.TryDecode(command.Version, out var version))
        {
            return UserErrors.VersionInvalid;
        }

        if (!await OrganisationLock.TryAcquireAsync(context, cancellationToken).ConfigureAwait(false))
        {
            return OrganisationLock.Busy;
        }

        if (await ActingPerson.IsInactiveAsync(context, actor, cancellationToken).ConfigureAwait(false))
        {
            return UserErrors.ActorInactive;
        }

        var id = UserId.From(command.Id);
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (user.EntraObjectId == actor.ActorId)
        {
            return UserErrors.OwnStatus;
        }

        context.Entry(user).Property(u => u.RowVersion).OriginalValue = version;
        if (command.Status == PersonStatus.Active)
        {
            user.Reactivate();

            return user.Id.Value;
        }

        var handover = await TeamHandoverPlan.PlanAsync(context, id, command.Team, IndianStandardTime.Today(timeProvider), cancellationToken).ConfigureAwait(false);
        if (handover.IsFailure)
        {
            return handover.Error!;
        }

        handover.Value.Apply(context);
        user.Deactivate(DeactivationReason.Administrator);

        return user.Id.Value;
    }
}
