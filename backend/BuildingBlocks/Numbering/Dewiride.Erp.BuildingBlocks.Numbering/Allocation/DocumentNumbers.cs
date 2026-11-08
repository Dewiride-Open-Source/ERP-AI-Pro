using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Numbering.Definitions;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;
using Dewiride.Erp.BuildingBlocks.Numbering.Rules;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Allocation;

// A financial year opens with its first number, in the format its numbering has then, checked against that year's rule, and
// starts at 1 unless its next number was set before; the year that opened stays as it opened. A command that takes several numbers
// finds the year its first number opened among the unit of work's tracked rows, because a query never returns a row not yet saved.
internal sealed class DocumentNumbers(NumberedDocumentCatalog catalog, ISerialNumberRules rules) : IDocumentNumbers
{
    public async Task<Result<DocumentNumber>> NextAsync(ModuleDbContext context, string documentType, DateOnly documentDate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var document = catalog.Find(documentType)
            ?? throw new InvalidOperationException($"{documentType} is not a numbered document; declare it with {nameof(NumberingServiceCollectionExtensions.AddNumberedDocument)} in its module's services.");
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException($"A number of {documentType} was asked for outside a unit of work; ask for it in the command that issues the document, so the number commits with the document.");
        }

        if (context.Model.FindEntityType(typeof(NumberSeries)) is null || context.Model.FindEntityType(typeof(NumberSeriesYear)) is null)
        {
            throw new InvalidOperationException($"{context.GetType().Name} maps no numbering; declare `public DbSet<NumberSeries> NumberSeries => Set<NumberSeries>();` and `public DbSet<NumberSeriesYear> NumberSeriesYears => Set<NumberSeriesYear>();`.");
        }

        if (!await NumberingLock.TryAcquireAsync(context, document, cancellationToken).ConfigureAwait(false))
        {
            return NumberingErrors.Busy(document);
        }

        var series = await context.Set<NumberSeries>().SingleOrDefaultAsync(s => s.DocumentType == document.Key, cancellationToken).ConfigureAwait(false);
        if (series is null)
        {
            return NumberingErrors.NotSetUp(document);
        }

        var year = FinancialYear.FromDate(documentDate);
        var opened = context.Set<NumberSeriesYear>().Local.FirstOrDefault(y => y.SeriesId == series.Id && y.Year == year)
            ?? await context.Set<NumberSeriesYear>().SingleOrDefaultAsync(y => y.SeriesId == series.Id && y.Year == year, cancellationToken).ConfigureAwait(false);
        if (opened is null)
        {
            var rule = await rules.RuleForAsync(document.SerialNumberRule, year, cancellationToken).ConfigureAwait(false);
            if (rule is null)
            {
                return NumberingErrors.NoRule(document, year);
            }

            if (series.Format.ProblemsUnder(rule, year).Count > 0)
            {
                return NumberingErrors.FormatNotAllowed(document, year);
            }

            opened = NumberSeriesYear.Open(series.Id, year, series.Format, rule.MaxLength, NumberSeriesYear.FirstOfYear);
            context.Set<NumberSeriesYear>().Add(opened);
        }

        if (!opened.TryIssue(out var sequence))
        {
            return NumberingErrors.Exhausted(document, opened);
        }

        return new DocumentNumber(opened.Format.Render(year, sequence), year, sequence);
    }
}
