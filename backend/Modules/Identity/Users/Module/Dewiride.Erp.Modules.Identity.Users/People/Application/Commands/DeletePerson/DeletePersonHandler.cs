using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Positions.Application;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.DeletePerson;

// A person is master data, so removing the record soft-deletes it; the person's sessions end at their next request, and a
// person who still holds an app role of the registration starts a new record at their next sign-in. A person others report to
// today or from a later date keeps their record until those people have another manager, so no position that holds names a
// deleted manager.
internal sealed class DeletePersonHandler(UsersDbContext context, IActorContext actor, TimeProvider timeProvider) : ICommandHandler<DeletePersonCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(DeletePersonCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await OrganisationLock.TryAcquireAsync(context, cancellationToken).ConfigureAwait(false))
        {
            return OrganisationLock.Busy;
        }

        var id = UserId.From(command.Id);
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (user.EntraObjectId == actor.ActorId)
        {
            return UserErrors.OwnDeletion;
        }

        if (await CountingPositions.On(context, IndianStandardTime.Today(timeProvider)).AnyAsync(p => p.ManagerId == id, cancellationToken).ConfigureAwait(false))
        {
            return UserErrors.ManagesPeople;
        }

        context.Users.Remove(user);

        return user.Id.Value;
    }
}
