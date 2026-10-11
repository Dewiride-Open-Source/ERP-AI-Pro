using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Positions.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Application;

// A position counts while it can still hold for someone: it is its person's position today or one they start later, and the
// person's record is not deleted. A position that a later one replaced on or before today is history, and the last position
// of a deleted person, which never ends, holds for nobody.
internal static class CountingPositions
{
    public static IQueryable<Position> On(UsersDbContext context, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Positions.Where(position =>
            context.Users.Any(user => user.Id == position.UserId)
            && !context.Positions.Any(later => later.UserId == position.UserId && later.EffectiveFrom > position.EffectiveFrom && later.EffectiveFrom <= today));
    }
}
