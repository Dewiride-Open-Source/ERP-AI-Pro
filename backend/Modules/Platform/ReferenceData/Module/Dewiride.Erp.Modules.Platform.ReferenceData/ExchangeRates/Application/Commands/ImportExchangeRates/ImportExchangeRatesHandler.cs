using System.Globalization;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Commands.ImportExchangeRates;

// A file is recorded whole or not at all: every row is checked first and each problem is keyed by its row, and a row equal
// to the rate already recorded for its currency and day is left as it is, so importing the same file twice records nothing
// the second time. A row that differs from a recorded rate is refused, because a correction is made on the rate itself.
internal sealed class ImportExchangeRatesHandler(ReferenceDataDbContext context, TimeProvider timeProvider) : ICommandHandler<ImportExchangeRatesCommand, ImportedExchangeRates>
{
    public async Task<Result<ImportedExchangeRates>> HandleAsync(ImportExchangeRatesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var today = IndianStandardTime.Today(timeProvider);
        var problems = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var candidates = new List<(int Row, ExchangeRate Rate)>();
        var firstRows = new Dictionary<(Currency Currency, DateOnly RateDate), int>();
        for (var row = 0; row < command.Rates.Count; row++)
        {
            var entry = command.Rates[row];
            if (!Currency.TryFromCode(entry.Currency, out var currency))
            {
                Report(problems, row, ExchangeRateErrors.CurrencyUnknown(entry.Currency));
                continue;
            }

            var recorded = ExchangeRate.Record(currency, entry.RateDate, entry.Rate, entry.Unit, entry.Source, today);
            if (recorded.IsFailure)
            {
                Report(problems, row, recorded.Error!);
                continue;
            }

            if (firstRows.TryGetValue((currency, entry.RateDate), out var first))
            {
                problems[Member(row, nameof(ImportedExchangeRate.RateDate))] = [Describe($"Row {first + 1} is already a rate of {currency} for {entry.RateDate:dd-MM-yyyy}.")];
                continue;
            }

            firstRows.Add((currency, entry.RateDate), row);
            candidates.Add((row, recorded.Value));
        }

        var dates = candidates.Select(candidate => candidate.Rate.RateDate).Distinct().ToList();
        var stored = await context.ExchangeRates
            .AsNoTracking()
            .Where(e => dates.Contains(e.RateDate))
            .ToDictionaryAsync(e => (e.Currency, e.RateDate), cancellationToken)
            .ConfigureAwait(false);

        var added = new List<ExchangeRate>();
        var unchanged = 0;
        foreach (var (row, rate) in candidates)
        {
            if (!stored.TryGetValue((rate.Currency, rate.RateDate), out var existing))
            {
                added.Add(rate);
            }
            else if (existing.Quotes(rate.Rate, rate.Unit))
            {
                unchanged++;
            }
            else
            {
                problems[Member(row, nameof(ImportedExchangeRate.Rate))] =
                    [Describe($"{rate.Currency} for {rate.RateDate:dd-MM-yyyy} is already recorded as {existing.Rate} rupees for {existing.Unit}; correct that rate instead.")];
            }
        }

        if (problems.Count > 0)
        {
            return Error.Validation(
                "exchange-rate.import-refused",
                Describe($"{problems.Count} rows of the file cannot be recorded, so no rate of the file was recorded."),
                problems);
        }

        context.ExchangeRates.AddRange(added);

        return new ImportedExchangeRates(added.Count, unchanged);
    }

    private static void Report(Dictionary<string, string[]> problems, int row, Error error)
    {
        foreach (var (member, messages) in error.Fields)
        {
            problems[Member(row, member)] = messages;
        }
    }

    private static string Member(int row, string member) =>
        string.Create(CultureInfo.InvariantCulture, $"rates[{row}].{JsonNamingPolicy.CamelCase.ConvertName(member)}");

    private static string Describe(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
