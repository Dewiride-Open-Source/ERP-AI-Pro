using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.Integration;

// The change key is the one the entity configuration declares, read from the model rather than written twice. A removed rate
// keeps its name, so its history still says which currency and day it was.
internal sealed class ExchangeRateRecordNames : IRecordNames
{
    private readonly ReferenceDataDbContext _context;

    private readonly string _key;

    public ExchangeRateRecordNames(ReferenceDataDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _key = context.Model.FindEntityType(typeof(ExchangeRate)) is { } recorded && RecordedEntityTypes.KeyOf(recorded) is { } key
            ? key
            : throw new InvalidOperationException($"{nameof(ReferenceDataDbContext)} maps no change key for {nameof(ExchangeRate)}.");
        EntityTypes = new HashSet<string>(StringComparer.Ordinal) { _key };
    }

    public IReadOnlySet<string> EntityTypes { get; }

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(string entityType, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (!string.Equals(entityType, _key, StringComparison.Ordinal))
        {
            throw new ArgumentException($"{nameof(ExchangeRateRecordNames)} names {_key} only, not {entityType}.", nameof(entityType));
        }

        var recordIds = ids.Select(ExchangeRateId.From).ToList();
        var named = await _context.ExchangeRates
            .IncludeDeleted()
            .AsNoTracking()
            .Where(e => recordIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Currency, e.RateDate })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return named.ToDictionary(e => e.Id.Value, e => string.Create(CultureInfo.InvariantCulture, $"{e.Currency} on {e.RateDate:dd-MM-yyyy}"));
    }
}
