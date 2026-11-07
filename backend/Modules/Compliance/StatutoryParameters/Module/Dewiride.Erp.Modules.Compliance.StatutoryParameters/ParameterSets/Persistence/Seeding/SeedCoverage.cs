using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Timeline;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;

// A row's value must hold on every day the row is in force: a code a column references is a row key of each referenced set on
// all of those days, through one row or several that follow each other, and a GST State code is in force in the reference data
// on all of them. A set may reference itself.
internal static class SeedCoverage
{
    public static IReadOnlyList<string> Problems(IReadOnlyList<SeededSet> sets, IReadOnlyList<IndianStateEntry> states)
    {
        ArgumentNullException.ThrowIfNull(sets);
        ArgumentNullException.ThrowIfNull(states);

        var byKey = sets.GroupBy(seeded => seeded.Set.Key, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var problems = new List<string>();
        foreach (var seeded in sets)
        {
            foreach (var (column, references) in seeded.References)
            {
                foreach (var reference in references)
                {
                    if (!byKey.TryGetValue(reference, out var target))
                    {
                        problems.Add($"{FileOf(seeded)}: column {column} references {reference}, which no seed file holds.");
                        continue;
                    }

                    problems.AddRange(Uncovered(seeded, column, value => target.Rows.Where(row => string.Equals(row.RowKey, value, StringComparison.Ordinal)).Select(row => row.Span))
                        .Select(row => $"{FileOf(seeded)}: row {row.RowKey} from {row.From:yyyy-MM-dd}, cell {column}: '{row.Cells[column]}' is not a row key of {reference} on every day {DaysOf(row)}."));
                }
            }

            foreach (var column in seeded.Set.Columns.Columns.Where(column => column.Kind == CellKind.StateCode))
            {
                problems.AddRange(Uncovered(seeded, column.Name, value => states.Where(state => string.Equals(state.GstCode, value, StringComparison.Ordinal)).Select(state => new DateSpan(state.ValidFrom, state.ValidUntil)))
                    .Select(row => $"{FileOf(seeded)}: row {row.RowKey} from {row.From:yyyy-MM-dd}, cell {column.Name}: GST State code {row.Cells[column.Name]} is not in force on every day {DaysOf(row)}."));
            }
        }

        return problems;
    }

    private static IEnumerable<ParameterRow> Uncovered(SeededSet seeded, string column, Func<string, IEnumerable<DateSpan>> spansOf) =>
        seeded.Rows.Where(row => row.Cells[column] is { } value && !DateSpan.IsCovered(row.Span, spansOf(value)));

    private static string FileOf(SeededSet seeded) => seeded.Set.Key + StatutorySeedFiles.FileExtension;

    private static string DaysOf(ParameterRow row) =>
        row.Until is { } until ? $"from {row.From:yyyy-MM-dd} to {until:yyyy-MM-dd}" : $"from {row.From:yyyy-MM-dd} on";
}
