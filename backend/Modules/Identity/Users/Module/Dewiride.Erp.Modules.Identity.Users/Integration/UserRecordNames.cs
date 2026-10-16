using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Dewiride.Erp.Modules.Identity.Users.Departments.Domain;
using Dewiride.Erp.Modules.Identity.Users.Designations.Domain;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.Integration;

// The history names people, departments and designations as their records name them today, deleted ones too, because a change
// stays in the history after its record is deleted. An actor is known by their Entra object id, which a deleted record and the
// record of the person's later return can both hold, so the record that is not deleted names them, else the newer one. The
// change keys are the ones the entity configurations declare, read from the model rather than written twice.
internal sealed class UserRecordNames : IRecordNames, IPersonNames
{
    private readonly UsersDbContext _context;

    private readonly string _people;

    private readonly string _departments;

    private readonly string _designations;

    public UserRecordNames(UsersDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _people = KeyOf<User>(context);
        _departments = KeyOf<Department>(context);
        _designations = KeyOf<Designation>(context);
        EntityTypes = new HashSet<string>(StringComparer.Ordinal) { _people, _departments, _designations };
    }

    public IReadOnlySet<string> EntityTypes { get; }

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(string entityType, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (string.Equals(entityType, _people, StringComparison.Ordinal))
        {
            var userIds = ids.Select(UserId.From).ToList();
            var people = await _context.Users
                .IncludeDeleted()
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.DisplayName })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return people.ToDictionary(u => u.Id.Value, u => u.DisplayName);
        }

        if (string.Equals(entityType, _departments, StringComparison.Ordinal))
        {
            var departmentIds = ids.Select(DepartmentId.From).ToList();
            var departments = await _context.Departments
                .IncludeDeleted()
                .AsNoTracking()
                .Where(d => departmentIds.Contains(d.Id))
                .Select(d => new { d.Id, d.Name })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return departments.ToDictionary(d => d.Id.Value, d => d.Name);
        }

        if (string.Equals(entityType, _designations, StringComparison.Ordinal))
        {
            var designationIds = ids.Select(DesignationId.From).ToList();
            var designations = await _context.Designations
                .IncludeDeleted()
                .AsNoTracking()
                .Where(d => designationIds.Contains(d.Id))
                .Select(d => new { d.Id, d.Name })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return designations.ToDictionary(d => d.Id.Value, d => d.Name);
        }

        throw new ArgumentException($"{nameof(UserRecordNames)} names {_people}, {_departments} and {_designations} only, not {entityType}.", nameof(entityType));
    }

    public async Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(IReadOnlyCollection<Guid> objectIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objectIds);

        var wanted = objectIds.Select(objectId => (Guid?)objectId).ToList();
        var records = await _context.Users
            .IncludeDeleted()
            .AsNoTracking()
            .Where(u => wanted.Contains(u.EntraObjectId))
            .Select(u => new { u.EntraObjectId, u.DisplayName, u.IsDeleted, u.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return records
            .GroupBy(record => record.EntraObjectId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(record => record.IsDeleted).ThenByDescending(record => record.CreatedAt).First().DisplayName);
    }

    private static string KeyOf<TEntity>(UsersDbContext context) =>
        context.Model.FindEntityType(typeof(TEntity)) is { } entityType && RecordedEntityTypes.KeyOf(entityType) is { } key
            ? key
            : throw new InvalidOperationException($"{nameof(UsersDbContext)} maps no change key for {typeof(TEntity).Name}.");
}
