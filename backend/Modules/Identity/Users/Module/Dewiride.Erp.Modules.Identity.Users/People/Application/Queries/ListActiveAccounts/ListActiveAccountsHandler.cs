using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.ListActiveAccounts;

// The Entra accounts of the people the ERP admits: the active records not deleted that a sign-in or an invitation linked to
// an account. A record registered by work email alone has no account to check until its person's first sign-in.
internal sealed class ListActiveAccountsHandler(UsersDbContext context) : IQueryHandler<ListActiveAccountsQuery, IReadOnlyList<Guid>>
{
    public async Task<Result<IReadOnlyList<Guid>>> HandleAsync(ListActiveAccountsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var accounts = await context.Users
            .AsNoTracking()
            .Where(u => u.EntraObjectId != null && u.Status == UserStatus.Active)
            .Select(u => u.EntraObjectId!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return accounts;
    }
}
