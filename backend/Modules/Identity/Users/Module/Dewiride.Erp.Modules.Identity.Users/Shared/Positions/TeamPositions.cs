using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Positions.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.Shared.Positions;

// A person's team is everyone with a position that names the person as manager and still counts: the position they hold
// today or one they start later. Someone whose positions named the manager only before today is outside the team.
internal static class TeamPositions
{
    public static IQueryable<Position> Of(UsersDbContext context, UserId manager, DateOnly today) =>
        CountingPositions.On(context, today).Where(position => position.ManagerId == manager);
}
