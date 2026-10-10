using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.DeletePerson;

// A person is master data, so removing the record soft-deletes it; the person's sessions end at their next request, and a
// person who still holds an app role of the registration starts a new record at their next sign-in.
internal sealed class DeletePersonHandler(UsersDbContext context, IActorContext actor) : ICommandHandler<DeletePersonCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(DeletePersonCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

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

        context.Users.Remove(user);

        return user.Id.Value;
    }
}
