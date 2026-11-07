using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Integration;

// The change keys are the ones the entity configurations declare, read from the model rather than written twice. A set is named by
// its title, and a version or row, filed under its set, by the set's title with its date and, for a row, its key; a version or
// row the seeder deleted has no name left.
internal sealed class ParameterSetRecordNames : IRecordNames
{
    private readonly StatutoryParametersDbContext _context;

    private readonly string _setKey;

    private readonly string _versionKey;

    private readonly string _rowKey;

    public ParameterSetRecordNames(StatutoryParametersDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _setKey = KeyOf(context, typeof(ParameterSet));
        _versionKey = KeyOf(context, typeof(ParameterVersion));
        _rowKey = KeyOf(context, typeof(ParameterRow));
        EntityTypes = new HashSet<string>(StringComparer.Ordinal) { _setKey, _versionKey, _rowKey };
    }

    public IReadOnlySet<string> EntityTypes { get; }

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(string entityType, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (string.Equals(entityType, _setKey, StringComparison.Ordinal))
        {
            var setIds = ids.Select(ParameterSetId.From).ToList();

            return await _context.ParameterSets
                .AsNoTracking()
                .Where(s => setIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id.Value, s => s.Title, cancellationToken)
                .ConfigureAwait(false);
        }

        if (string.Equals(entityType, _versionKey, StringComparison.Ordinal))
        {
            var versionIds = ids.Select(ParameterVersionId.From).ToList();
            var versions = await _context.ParameterVersions
                .AsNoTracking()
                .Where(v => versionIds.Contains(v.Id))
                .Join(_context.ParameterSets, v => v.SetId, s => s.Id, (v, s) => new { v.Id, s.Title, v.From })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return versions.ToDictionary(v => v.Id.Value, v => string.Create(CultureInfo.InvariantCulture, $"{v.Title}, version from {v.From:dd-MM-yyyy}"));
        }

        if (string.Equals(entityType, _rowKey, StringComparison.Ordinal))
        {
            var rowIds = ids.Select(ParameterRowId.From).ToList();
            var rows = await _context.ParameterRows
                .AsNoTracking()
                .Where(r => rowIds.Contains(r.Id))
                .Join(_context.ParameterSets, r => r.SetId, s => s.Id, (r, s) => new { r.Id, s.Title, r.RowKey, r.From })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return rows.ToDictionary(r => r.Id.Value, r => string.Create(CultureInfo.InvariantCulture, $"{r.Title}: {r.RowKey} from {r.From:dd-MM-yyyy}"));
        }

        throw new ArgumentException($"{nameof(ParameterSetRecordNames)} names {_setKey}, {_versionKey} and {_rowKey} only, not {entityType}.", nameof(entityType));
    }

    private static string KeyOf(StatutoryParametersDbContext context, Type entityType) =>
        context.Model.FindEntityType(entityType) is { } recorded && RecordedEntityTypes.KeyOf(recorded) is { } key
            ? key
            : throw new InvalidOperationException($"{nameof(StatutoryParametersDbContext)} maps no change key for {entityType.Name}.");
}
