using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Profile.Application.Commands.ChooseOwnTheme;

// The theme is the person's own setting, so the latest choice wins and the command names no version of the record.
internal sealed class ChooseOwnThemeHandler(UsersDbContext context, IActorContext actor) : ICommandHandler<ChooseOwnThemeCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(ChooseOwnThemeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var objectId = actor.ActorId;
        var user = await context.Users.SingleOrDefaultAsync(u => u.EntraObjectId == objectId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return UserErrors.OwnRecordNotFound;
        }

        user.ChooseTheme(ThemePreferences.ToUserTheme(command.Theme));

        return user.Id.Value;
    }
}
