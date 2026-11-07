namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;

// The columns a set declares, in the order its file lists them. Exactly one column is the key, whose value is the row's key,
// and at most one titles a row.
internal sealed record ParameterColumnSet
{
    private ParameterColumnSet(IReadOnlyList<ParameterColumn> columns, ParameterColumn keyColumn)
    {
        Columns = columns;
        KeyColumn = keyColumn;
    }

    public IReadOnlyList<ParameterColumn> Columns { get; }

    public ParameterColumn KeyColumn { get; }

    public static ParameterColumnSet Create(IEnumerable<ParameterColumn> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        var list = columns.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("A set declares at least one column.", nameof(columns));
        }

        if (list.GroupBy(column => column.Name, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1) is { } repeated)
        {
            throw new ArgumentException($"The set declares column {repeated.Key} twice.", nameof(columns));
        }

        var keys = list.Where(column => column.IsKey).ToList();
        if (keys.Count != 1)
        {
            throw new ArgumentException($"A set declares exactly one key column, not {keys.Count}.", nameof(columns));
        }

        var rowTitles = list.Count(column => column.IsRowTitle);
        if (rowTitles > 1)
        {
            throw new ArgumentException($"A set declares at most one column that titles a row, not {rowTitles}.", nameof(columns));
        }

        return new ParameterColumnSet(list, keys[0]);
    }

    public ParameterColumn? Find(string name) => Columns.FirstOrDefault(column => string.Equals(column.Name, name, StringComparison.Ordinal));

    public IReadOnlyList<string> ProblemsOf(ParameterCells cells)
    {
        ArgumentNullException.ThrowIfNull(cells);

        var problems = Columns
            .Where(column => !cells.Holds(column.Name))
            .Select(column => $"has no cell of column {column.Name}")
            .Concat(cells.Values.Where(cell => Find(cell.Key) is null).Select(cell => $"has a cell of {cell.Key}, which the set does not declare"))
            .ToList();
        foreach (var column in Columns.Where(column => cells.Holds(column.Name)))
        {
            if (column.Problem(cells[column.Name]) is { } problem)
            {
                problems.Add($"cell {column.Name}: {problem}");
            }
        }

        return problems;
    }

    public bool Equals(ParameterColumnSet? other) => other is not null && Columns.SequenceEqual(other.Columns);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var column in Columns)
        {
            hash.Add(column);
        }

        return hash.ToHashCode();
    }
}
