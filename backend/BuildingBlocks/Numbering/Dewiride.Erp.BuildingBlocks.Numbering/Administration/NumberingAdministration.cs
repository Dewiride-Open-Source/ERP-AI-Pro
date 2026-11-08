using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Numbering.Allocation;
using Dewiride.Erp.BuildingBlocks.Numbering.Definitions;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;
using Dewiride.Erp.BuildingBlocks.Numbering.Persistence;
using Dewiride.Erp.BuildingBlocks.Numbering.Rules;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Administration;

// Each change runs in a unit of work of its own on the numbering's context and takes the lock a number takes, so it never
// interleaves with the numbering of a document of that kind. A numbering is read before its lock is taken, to learn its kind, so
// the version the change was made from becomes the original rowversion and saving refuses one that changed meanwhile.
internal sealed class NumberingAdministration(
    NumberingDbContext context,
    NumberedDocumentCatalog catalog,
    ISerialNumberRules rules,
    DomainEventDispatcher dispatcher,
    UnitOfWorkSignal signal,
    TimeProvider timeProvider) : INumberingAdministration
{
    public async Task<IReadOnlyList<NumberedDocumentDetails>> ListAsync(CancellationToken cancellationToken)
    {
        var current = FinancialYear.Current(timeProvider);
        var series = await context.Series.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var years = await context.Years.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

        var details = new List<NumberedDocumentDetails>(catalog.Documents.Count);
        foreach (var document in catalog.Documents)
        {
            var rule = await rules.RuleForAsync(document.SerialNumberRule, current, cancellationToken).ConfigureAwait(false);
            var numbering = series.SingleOrDefault(s => string.Equals(s.DocumentType, document.Key, StringComparison.Ordinal));
            details.Add(new NumberedDocumentDetails(document, current, rule, numbering is null ? null : DetailsOf(numbering, years)));
        }

        return details;
    }

    public async Task<Result<NumberedDocumentDetails>> GetAsync(NumberSeriesId id, CancellationToken cancellationToken)
    {
        var series = await context.Series.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false);
        var document = series is null ? null : catalog.Find(series.DocumentType);

        return document is null ? NumberingErrors.SeriesNotFound : await DetailsOfAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<NumberedDocumentDetails>> SetUpAsync(string documentType, NumberFormat format, long nextNumber, CancellationToken cancellationToken)
    {
        var document = catalog.Find(documentType);
        if (document is null)
        {
            return NumberingErrors.UnknownDocument(documentType);
        }

        var current = FinancialYear.Current(timeProvider);
        var saved = await EfUnitOfWork.RunAsync<NumberSeriesId>(
            context,
            dispatcher,
            signal,
            async token =>
            {
                if (!await NumberingLock.TryAcquireAsync(context, document, token).ConfigureAwait(false))
                {
                    return NumberingErrors.Busy(document);
                }

                if (await context.Series.AnyAsync(s => s.DocumentType == document.Key, token).ConfigureAwait(false))
                {
                    return NumberingErrors.AlreadySetUp(document);
                }

                var rule = await rules.RuleForAsync(document.SerialNumberRule, current, token).ConfigureAwait(false);
                if (rule is null)
                {
                    return NumberingErrors.NoRule(document, current);
                }

                if ((ProblemOf(format, rule, current) ?? NextNumberProblemOf(format, rule, nextNumber)) is { } problem)
                {
                    return problem;
                }

                var series = NumberSeries.Create(document.Key, format);
                context.Series.Add(series);
                context.Years.Add(NumberSeriesYear.Open(series.Id, current, format, rule.MaxLength, nextNumber));

                return series.Id;
            },
            cancellationToken).ConfigureAwait(false);

        return saved.IsFailure ? saved.Error! : await DetailsOfAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<NumberedDocumentDetails>> ChangeAsync(NumberSeriesId id, byte[] version, NumberFormat format, long? nextNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);

        var current = FinancialYear.Current(timeProvider);
        NumberedDocument? changed = null;
        var saved = await EfUnitOfWork.RunAsync<NumberSeriesId>(
            context,
            dispatcher,
            signal,
            async token =>
            {
                var series = await context.Series.SingleOrDefaultAsync(s => s.Id == id, token).ConfigureAwait(false);
                var document = series is null ? null : catalog.Find(series.DocumentType);
                if (series is null || document is null)
                {
                    return NumberingErrors.SeriesNotFound;
                }

                changed = document;
                if (!await NumberingLock.TryAcquireAsync(context, document, token).ConfigureAwait(false))
                {
                    return NumberingErrors.Busy(document);
                }

                context.Entry(series).Property(s => s.RowVersion).OriginalValue = version;
                var rule = await rules.RuleForAsync(document.SerialNumberRule, current, token).ConfigureAwait(false);
                if (rule is null)
                {
                    return NumberingErrors.NoRule(document, current);
                }

                if (ProblemOf(format, rule, current) is { } formatProblem)
                {
                    return formatProblem;
                }

                var opened = await context.Years.SingleOrDefaultAsync(y => y.SeriesId == series.Id && y.Year == current, token).ConfigureAwait(false);
                if (opened is { InUse: true })
                {
                    if (nextNumber is { } asked && asked != opened.NextNumber)
                    {
                        return NumberingErrors.YearInUse(current);
                    }

                    series.Change(format);
                    return series.Id;
                }

                var next = nextNumber ?? opened?.FirstNumber ?? NumberSeriesYear.FirstOfYear;
                if (NextNumberProblemOf(format, rule, next) is { } nextProblem)
                {
                    return nextProblem;
                }

                series.Change(format);
                if (opened is null)
                {
                    context.Years.Add(NumberSeriesYear.Open(series.Id, current, format, rule.MaxLength, next));
                }
                else
                {
                    opened.Reopen(format, rule.MaxLength, next);
                }

                return series.Id;
            },
            cancellationToken).ConfigureAwait(false);

        return saved.IsFailure ? saved.Error! : await DetailsOfAsync(changed!, cancellationToken).ConfigureAwait(false);
    }

    private static Error? ProblemOf(NumberFormat format, SerialNumberRule rule, FinancialYear year)
    {
        var problems = format.ProblemsUnder(rule, year);

        return problems.Count > 0 ? NumberingErrors.FormatInvalid(problems) : null;
    }

    private static Error? NextNumberProblemOf(NumberFormat format, SerialNumberRule rule, long nextNumber)
    {
        var last = format.LastNumberWithin(rule.MaxLength);

        return nextNumber < NumberSeriesYear.FirstOfYear || nextNumber > last ? NumberingErrors.NextNumberOutOfRange(last) : null;
    }

    private static NumberSeriesDetails DetailsOf(NumberSeries series, IEnumerable<NumberSeriesYear> years) =>
        new(
            series.Id,
            series.Format,
            RecordVersion.Encode(series.RowVersion),
            [.. years.Where(y => y.SeriesId == series.Id).OrderByDescending(y => y.Year).Select(NumberSeriesYearDetails.Of)]);

    private async Task<NumberedDocumentDetails> DetailsOfAsync(NumberedDocument document, CancellationToken cancellationToken)
    {
        var current = FinancialYear.Current(timeProvider);
        var rule = await rules.RuleForAsync(document.SerialNumberRule, current, cancellationToken).ConfigureAwait(false);
        var series = await context.Series.AsNoTracking().SingleOrDefaultAsync(s => s.DocumentType == document.Key, cancellationToken).ConfigureAwait(false);
        if (series is null)
        {
            return new NumberedDocumentDetails(document, current, rule, null);
        }

        var years = await context.Years.AsNoTracking().Where(y => y.SeriesId == series.Id).ToListAsync(cancellationToken).ConfigureAwait(false);

        return new NumberedDocumentDetails(document, current, rule, DetailsOf(series, years));
    }
}
