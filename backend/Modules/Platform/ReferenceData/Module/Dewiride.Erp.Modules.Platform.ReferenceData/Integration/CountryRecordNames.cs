using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.Integration;

// The change key is the one the entity configuration declares, read from the model rather than written twice.
internal sealed class CountryRecordNames : IRecordNames
{
    private readonly ReferenceDataDbContext _context;

    private readonly string _key;

    public CountryRecordNames(ReferenceDataDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _key = context.Model.FindEntityType(typeof(Country)) is { } recorded && RecordedEntityTypes.KeyOf(recorded) is { } key
            ? key
            : throw new InvalidOperationException($"{nameof(ReferenceDataDbContext)} maps no change key for {nameof(Country)}.");
        EntityTypes = new HashSet<string>(StringComparer.Ordinal) { _key };
    }

    public IReadOnlySet<string> EntityTypes { get; }

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(string entityType, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (!string.Equals(entityType, _key, StringComparison.Ordinal))
        {
            throw new ArgumentException($"{nameof(CountryRecordNames)} names {_key} only, not {entityType}.", nameof(entityType));
        }

        var recordIds = ids.Select(CountryId.From).ToList();
        var named = await _context.Countries
            .AsNoTracking()
            .Where(c => recordIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return named.ToDictionary(c => c.Id.Value, c => c.Name);
    }
}
