using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.IsPersonAdmitted;

internal sealed class IsPersonAdmittedHandler(UsersDbContext context) : IQueryHandler<IsPersonAdmittedQuery, bool>
{
    public async Task<Result<bool>> HandleAsync(IsPersonAdmittedQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await context.Users
            .AsNoTracking()
            .AnyAsync(u => u.EntraObjectId == query.EntraObjectId && u.Status == UserStatus.Active, cancellationToken)
            .ConfigureAwait(false);
    }
}
