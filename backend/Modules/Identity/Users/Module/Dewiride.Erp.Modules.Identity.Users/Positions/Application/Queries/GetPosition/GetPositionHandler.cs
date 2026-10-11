using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Positions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Application.Queries.GetPosition;

internal sealed class GetPositionHandler(UsersDbContext context) : IQueryHandler<GetPositionQuery, PositionDetails>
{
    public async Task<Result<PositionDetails>> HandleAsync(GetPositionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var person = UserId.From(query.PersonId);
        if (!await context.Users.AnyAsync(u => u.Id == person, cancellationToken).ConfigureAwait(false))
        {
            return UserErrors.NotFound;
        }

        var id = PositionId.From(query.PositionId);
        var row = await PositionsWithNames.Of(context, person)
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Position.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? PositionErrors.NotFound : PositionDetails.From(row);
    }
}
