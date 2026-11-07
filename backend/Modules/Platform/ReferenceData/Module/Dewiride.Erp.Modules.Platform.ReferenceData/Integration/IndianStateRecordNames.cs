using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.Integration;

// The change key is the one the entity configuration declares, read from the model rather than written twice. A code names
// different territories over time, so a row is named by its code and name together.
internal sealed class IndianStateRecordNames : IRecordNames
{
    private readonly ReferenceDataDbContext _context;

    private readonly string _key;

    public IndianStateRecordNames(ReferenceDataDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _key = context.Model.FindEntityType(typeof(IndianState)) is { } recorded && RecordedEntityTypes.KeyOf(recorded) is { } key
            ? key
            : throw new InvalidOperationException($"{nameof(ReferenceDataDbContext)} maps no change key for {nameof(IndianState)}.");
        EntityTypes = new HashSet<string>(StringComparer.Ordinal) { _key };
    }

    public IReadOnlySet<string> EntityTypes { get; }

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(string entityType, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (!string.Equals(entityType, _key, StringComparison.Ordinal))
        {
            throw new ArgumentException($"{nameof(IndianStateRecordNames)} names {_key} only, not {entityType}.", nameof(entityType));
        }

        var recordIds = ids.Select(IndianStateId.From).ToList();
        var named = await _context.IndianStates
            .AsNoTracking()
            .Where(s => recordIds.Contains(s.Id))
            .Select(s => new { s.Id, s.GstCode, s.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return named.ToDictionary(s => s.Id.Value, s => $"{s.GstCode} {s.Name}");
    }
}
