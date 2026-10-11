using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.Designations.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Application.Queries.GetDesignation;

internal sealed class GetDesignationHandler(UsersDbContext context) : IQueryHandler<GetDesignationQuery, DesignationDetails>
{
    public async Task<Result<DesignationDetails>> HandleAsync(GetDesignationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var id = DesignationId.From(query.Id);
        var designation = await context.Designations.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, cancellationToken).ConfigureAwait(false);

        return designation is null ? DesignationErrors.NotFound : DesignationDetails.From(designation);
    }
}
