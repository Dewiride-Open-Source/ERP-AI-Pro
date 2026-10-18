using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application;

// The acting administrator always holds Erp.Admin, so the ERP loses its last administrator only when two of them deactivate or
// delete each other at once, each while their own record still admits them. A deactivation, a deletion or the directory check
// therefore reads the records of the person making it again, under the organisation lock that runs those writes one after the
// other, and is refused when every record naming that person is deactivated or deleted, as their next request would be. An
// actor no record names, such as the system, is not checked.
internal static class ActingPerson
{
    public static async Task<bool> IsInactiveAsync(UsersDbContext context, IActorContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actor);

        var actorId = actor.ActorId;
        var records = await context.Users
            .IncludeDeleted()
            .AsNoTracking()
            .Where(u => u.EntraObjectId == actorId)
            .Select(u => new { u.IsDeleted, u.Status })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return records.Count > 0 && !records.Exists(record => !record.IsDeleted && record.Status == UserStatus.Active);
    }
}
