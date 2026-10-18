using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Shared.Positions;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Profile.Application.Queries.GetOwnProfile;

// The caller's record is the one their Entra object id is linked to; a sign-in admits a person only on such a record, so only
// a caller who never signed in, such as one a test header names, has none.
internal sealed class GetOwnProfileHandler(UsersDbContext context, IActorContext actor, TimeProvider timeProvider) : IQueryHandler<GetOwnProfileQuery, OwnProfile>
{
    public async Task<Result<OwnProfile>> HandleAsync(GetOwnProfileQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var objectId = actor.ActorId;
        var row = await PeopleWithPositions.On(context, IndianStandardTime.Today(timeProvider))
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.User.EntraObjectId == objectId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return UserErrors.OwnRecordNotFound;
        }

        var user = row.User;

        return new OwnProfile(
            user.Id.Value,
            user.DisplayName,
            user.WorkEmail,
            user.EmployeeCode,
            user.PhoneNumber,
            user.DateOfJoining,
            row.DepartmentName,
            row.DesignationName,
            row.ManagerDisplayName,
            [.. Enum.GetValues<ErpRole>().Where(query.HeldRoles.Contains)],
            ThemePreferences.ToPreference(user.Theme));
    }
}
