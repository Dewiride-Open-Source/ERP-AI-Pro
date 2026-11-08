using Dewiride.Erp.BuildingBlocks.Numbering.Definitions;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Persistence;

// A numbering is named by the documents it numbers, as this release titles them, and a year by those documents and the year; a
// numbering of a kind of document no release declares is named by its key.
internal sealed class NumberingRecordNames(NumberingDbContext context, NumberedDocumentCatalog catalog) : IRecordNames
{
    public IReadOnlySet<string> EntityTypes { get; } = new HashSet<string>(StringComparer.Ordinal) { NumberingTables.SeriesChangeKey, NumberingTables.YearsChangeKey };

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(string entityType, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (string.Equals(entityType, NumberingTables.SeriesChangeKey, StringComparison.Ordinal))
        {
            var seriesIds = ids.Select(NumberSeriesId.From).ToList();
            var series = await context.Series
                .AsNoTracking()
                .Where(s => seriesIds.Contains(s.Id))
                .Select(s => new { s.Id, s.DocumentType })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return series.ToDictionary(s => s.Id.Value, s => TitleOf(s.DocumentType));
        }

        if (string.Equals(entityType, NumberingTables.YearsChangeKey, StringComparison.Ordinal))
        {
            var yearIds = ids.Select(NumberSeriesYearId.From).ToList();
            var years = await context.Years
                .AsNoTracking()
                .Where(y => yearIds.Contains(y.Id))
                .Join(context.Series.AsNoTracking(), y => y.SeriesId, s => s.Id, (y, s) => new { y.Id, y.Year, s.DocumentType })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return years.ToDictionary(y => y.Id.Value, y => $"{TitleOf(y.DocumentType)} {y.Year}");
        }

        throw new ArgumentException($"{nameof(NumberingRecordNames)} names {NumberingTables.SeriesChangeKey} and {NumberingTables.YearsChangeKey} only, not {entityType}.", nameof(entityType));
    }

    private string TitleOf(string documentType) => catalog.Find(documentType)?.Title ?? documentType;
}
