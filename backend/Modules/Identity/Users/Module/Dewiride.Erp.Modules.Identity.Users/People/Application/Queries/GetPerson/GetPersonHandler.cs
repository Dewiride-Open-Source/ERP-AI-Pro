using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Shared.Positions;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.GetPerson;

internal sealed class GetPersonHandler(UsersDbContext context, TimeProvider timeProvider) : IQueryHandler<GetPersonQuery, PersonDetails>
{
    public async Task<Result<PersonDetails>> HandleAsync(GetPersonQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var id = UserId.From(query.Id);
        var row = await PeopleWithPositions.On(context, IndianStandardTime.Today(timeProvider))
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.User.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? UserErrors.NotFound : PersonDetails.From(row);
    }
}
