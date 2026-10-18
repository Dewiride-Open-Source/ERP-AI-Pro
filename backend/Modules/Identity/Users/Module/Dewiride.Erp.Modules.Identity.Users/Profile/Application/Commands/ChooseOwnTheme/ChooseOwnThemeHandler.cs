using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Profile.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Profile.Application.Commands.ChooseOwnTheme;

// The theme is the person's own setting, so the latest choice wins and the command names no version. A person who never
// chose a theme follows the theme of their device, so choosing that theme first keeps no row. Two first choices saved at once
// both add the person's row, and the one saved second answers a conflict.
internal sealed class ChooseOwnThemeHandler(UsersDbContext context, IActorContext actor) : ICommandHandler<ChooseOwnThemeCommand, ChosenTheme>
{
    public async Task<Result<ChosenTheme>> HandleAsync(ChooseOwnThemeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var objectId = actor.ActorId;
        var userId = await context.Users
            .Where(u => u.EntraObjectId == objectId)
            .Select(u => (UserId?)u.Id)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (userId is not { } id)
        {
            return UserErrors.OwnRecordNotFound;
        }

        var theme = ThemePreferences.ToUserTheme(command.Theme);
        var chosen = await context.PersonThemes.SingleOrDefaultAsync(t => t.Id == id, cancellationToken).ConfigureAwait(false);
        if (chosen is not null)
        {
            chosen.Choose(theme);
        }
        else if (theme != UserTheme.System)
        {
            chosen = PersonTheme.ChooseFirst(id, theme);
            context.PersonThemes.Add(chosen);
        }

        return ThemePreferences.Of(chosen);
    }
}
