using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.GetPerson;

internal sealed class GetPersonHandler(UsersDbContext context) : IQueryHandler<GetPersonQuery, PersonDetails>
{
    public async Task<Result<PersonDetails>> HandleAsync(GetPersonQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var id = UserId.From(query.Id);
        var user = await context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken).ConfigureAwait(false);

        return user is null ? UserErrors.NotFound : PersonDetails.From(user);
    }
}
