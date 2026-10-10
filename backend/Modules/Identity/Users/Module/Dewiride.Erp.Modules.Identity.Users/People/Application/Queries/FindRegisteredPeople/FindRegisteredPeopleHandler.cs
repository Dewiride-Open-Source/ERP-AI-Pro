using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.FindRegisteredPeople;

// The ERP holds a directory person when it has a record their sign-in would be admitted with (AdmitPersonHandler): the
// record linked to their object id, else a record linked to no Entra account whose work email is their sign-in name.
internal sealed class FindRegisteredPeopleHandler(UsersDbContext context) : IQueryHandler<FindRegisteredPeopleQuery, IReadOnlyDictionary<Guid, Guid>>
{
    public async Task<Result<IReadOnlyDictionary<Guid, Guid>>> HandleAsync(FindRegisteredPeopleQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var registered = new Dictionary<Guid, Guid>();
        if (query.People.Count == 0)
        {
            return registered;
        }

        List<Guid?> objectIds = [.. query.People.Select(person => (Guid?)person.ObjectId)];
        List<string> workEmails = [.. query.People.Select(person => User.NormaliseWorkEmail(person.UserPrincipalName))];
        var records = await context.Users
            .AsNoTracking()
            .Where(u => objectIds.Contains(u.EntraObjectId) || (u.EntraObjectId == null && workEmails.Contains(u.WorkEmail)))
            .Select(u => new { u.Id, u.EntraObjectId, u.WorkEmail })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var person in query.People)
        {
            var workEmail = User.NormaliseWorkEmail(person.UserPrincipalName);
            var record = records.Find(r => r.EntraObjectId == person.ObjectId) ?? records.Find(r => r.EntraObjectId is null && r.WorkEmail == workEmail);
            if (record is not null)
            {
                registered[person.ObjectId] = record.Id.Value;
            }
        }

        return registered;
    }
}
