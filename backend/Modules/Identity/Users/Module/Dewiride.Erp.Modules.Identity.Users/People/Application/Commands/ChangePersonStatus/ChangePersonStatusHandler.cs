using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.ChangePersonStatus;

// An administrator who deactivated their own record would be signed out at once, perhaps with no other administrator left
// to reactivate it.
internal sealed class ChangePersonStatusHandler(UsersDbContext context, IActorContext actor) : ICommandHandler<ChangePersonStatusCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(ChangePersonStatusCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!PersonVersion.TryDecode(command.Version, out var version))
        {
            return UserErrors.VersionInvalid;
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
        if (command.Status == PersonStatus.Deactivated)
        {
            user.Deactivate();
        }
        else
        {
            user.Reactivate();
        }

        return user.Id.Value;
    }
}
