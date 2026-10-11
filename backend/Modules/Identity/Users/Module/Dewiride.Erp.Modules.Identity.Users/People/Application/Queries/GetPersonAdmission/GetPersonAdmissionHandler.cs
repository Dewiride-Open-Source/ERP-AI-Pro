using System.Collections.Frozen;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.GetPersonAdmission;

// One row answers both whether the person is admitted and which roles their record holds, so the permissions of every
// request cost no read of their own.
internal sealed class GetPersonAdmissionHandler(UsersDbContext context) : IQueryHandler<GetPersonAdmissionQuery, PersonAdmission?>
{
    public async Task<Result<PersonAdmission?>> HandleAsync(GetPersonAdmissionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var admitted = await context.Users
            .AsNoTracking()
            .Where(u => u.EntraObjectId == query.EntraObjectId && u.Status == UserStatus.Active)
            .Select(u => new { u.Roles })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        PersonAdmission? admission = admitted is null ? null : new PersonAdmission(admitted.Roles.ToFrozenSet());

        return admission;
    }
}
