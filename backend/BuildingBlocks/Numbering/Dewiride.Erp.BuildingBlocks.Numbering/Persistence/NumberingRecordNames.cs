using Dewiride.Erp.BuildingBlocks.Numbering.Definitions;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Persistence;

// A numbering is named by the documents it numbers, as this release titles them, and a year by those documents and the year; a
// numbering of a kind of document no release declares is named by its key. The change keys are the ones the entity
// configurations declare, read from the model rather than written twice.
internal sealed class NumberingRecordNames : IRecordNames
{
    private readonly NumberingDbContext _context;

    private readonly NumberedDocumentCatalog _catalog;

    private readonly string _series;

    private readonly string _years;

    public NumberingRecordNames(NumberingDbContext context, NumberedDocumentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);

        _context = context;
        _catalog = catalog;
        _series = KeyOf<NumberSeries>(context);
        _years = KeyOf<NumberSeriesYear>(context);
        EntityTypes = new HashSet<string>(StringComparer.Ordinal) { _series, _years };
    }

    public IReadOnlySet<string> EntityTypes { get; }

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(string entityType, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (string.Equals(entityType, _series, StringComparison.Ordinal))
        {
            var seriesIds = ids.Select(NumberSeriesId.From).ToList();
            var series = await _context.Series
                .AsNoTracking()
                .Where(s => seriesIds.Contains(s.Id))
                .Select(s => new { s.Id, s.DocumentType })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return series.ToDictionary(s => s.Id.Value, s => TitleOf(s.DocumentType));
        }

        if (string.Equals(entityType, _years, StringComparison.Ordinal))
        {
            var yearIds = ids.Select(NumberSeriesYearId.From).ToList();
            var years = await _context.Years
                .AsNoTracking()
                .Where(y => yearIds.Contains(y.Id))
                .Join(_context.Series.AsNoTracking(), y => y.SeriesId, s => s.Id, (y, s) => new { y.Id, y.Year, s.DocumentType })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return years.ToDictionary(y => y.Id.Value, y => $"{TitleOf(y.DocumentType)} {y.Year}");
        }

        throw new ArgumentException($"{nameof(NumberingRecordNames)} names {_series} and {_years} only, not {entityType}.", nameof(entityType));
    }

    private static string KeyOf<TEntity>(NumberingDbContext context) =>
        context.Model.FindEntityType(typeof(TEntity)) is { } entityType && RecordedEntityTypes.KeyOf(entityType) is { } key
            ? key
            : throw new InvalidOperationException($"{nameof(NumberingDbContext)} maps no change key for {typeof(TEntity).Name}.");

    private string TitleOf(string documentType) => _catalog.Find(documentType)?.Title ?? documentType;
}
