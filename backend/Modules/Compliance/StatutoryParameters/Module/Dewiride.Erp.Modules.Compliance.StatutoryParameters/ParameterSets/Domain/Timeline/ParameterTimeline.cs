namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Timeline;

// A set's versions are dates in increasing order, each running to the day before the next and the last with no end, so after
// the first there is neither a gap nor an overlap and before it the set holds nothing. A row starts on a version's date and,
// when it ends, ends the day before a later version's date, so the rows in force are the same on every day of a version. Rows
// of one key never overlap, though a key may stop and return, and every version changes something: a row starts on its date or
// ends the day before.
internal sealed class ParameterTimeline
{
    private readonly List<DateOnly> _declared;

    private readonly List<DateOnly> _versions;

    private readonly List<ParameterRow> _rows;

    public ParameterTimeline(IEnumerable<DateOnly> versions, IEnumerable<ParameterRow> rows)
    {
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(rows);

        _declared = [.. versions];
        _versions = [.. _declared.Distinct().Order()];
        _rows = [.. rows];
    }

    public IReadOnlyList<DateOnly> Versions => _versions;

    public IReadOnlyList<string> Problems()
    {
        if (_declared.Count == 0)
        {
            return ["The set declares no version."];
        }

        var problems = new List<string>();
        for (var index = 1; index < _declared.Count; index++)
        {
            if (_declared[index] <= _declared[index - 1])
            {
                problems.Add($"The version from {_declared[index]:yyyy-MM-dd} is not later than the version before it.");
            }
        }

        var dates = _versions.ToHashSet();
        foreach (var row in _rows)
        {
            if (!dates.Contains(row.From))
            {
                problems.Add($"Row {row.RowKey} starts on {row.From:yyyy-MM-dd}, which is no version's date.");
            }

            if (row.Until is { } until && (until == DateOnly.MaxValue || !dates.Contains(until.AddDays(1))))
            {
                problems.Add($"Row {row.RowKey} from {row.From:yyyy-MM-dd} ends on {until:yyyy-MM-dd}, which is not the day before a later version's date.");
            }
        }

        foreach (var rows in _rows.GroupBy(row => row.RowKey, StringComparer.Ordinal))
        {
            var ordered = rows.OrderBy(row => row.From).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                if (ordered[index - 1].Span.Overlaps(ordered[index].Span))
                {
                    problems.Add($"The rows of key {rows.Key} from {ordered[index - 1].From:yyyy-MM-dd} and from {ordered[index].From:yyyy-MM-dd} overlap.");
                }
            }
        }

        if (problems.Count > 0)
        {
            return problems;
        }

        for (var index = 0; index < _versions.Count; index++)
        {
            if (ChangesAt(_versions[index]).IsEmpty)
            {
                problems.Add(index == 0
                    ? $"The first version, from {_versions[index]:yyyy-MM-dd}, starts no row."
                    : $"The version from {_versions[index]:yyyy-MM-dd} changes nothing: no row starts on it or ends the day before.");
            }
        }

        return problems;
    }

    public DateOnly? VersionOn(DateOnly day) => _versions.Where(version => version <= day).Select(version => (DateOnly?)version).LastOrDefault();

    public DateOnly? NextAfter(DateOnly day) => _versions.Where(version => version > day).Select(version => (DateOnly?)version).FirstOrDefault();

    public DateOnly? UntilOf(DateOnly version) => NextAfter(version)?.AddDays(-1);

    public IReadOnlyList<ParameterRow> RowsOn(DateOnly day) =>
        [.. _rows.Where(row => row.Span.Contains(day)).OrderBy(row => row.RowKey, StringComparer.Ordinal)];

    public VersionChanges ChangesAt(DateOnly version)
    {
        var starting = _rows.Where(row => row.From == version).ToDictionary(row => row.RowKey, StringComparer.Ordinal);
        var ending = version == DateOnly.MinValue
            ? []
            : _rows.Where(row => row.Until == version.AddDays(-1)).ToDictionary(row => row.RowKey, StringComparer.Ordinal);

        return new VersionChanges(
            [.. starting.Where(row => !ending.ContainsKey(row.Key)).OrderBy(row => row.Key, StringComparer.Ordinal).Select(row => row.Value)],
            [.. starting.Where(row => ending.ContainsKey(row.Key)).OrderBy(row => row.Key, StringComparer.Ordinal).Select(row => new RowChange(ending[row.Key], row.Value))],
            [.. ending.Where(row => !starting.ContainsKey(row.Key)).OrderBy(row => row.Key, StringComparer.Ordinal).Select(row => row.Value)]);
    }
}
