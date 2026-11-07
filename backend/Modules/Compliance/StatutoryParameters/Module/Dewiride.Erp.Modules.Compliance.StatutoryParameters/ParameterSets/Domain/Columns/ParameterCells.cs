namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;

// The cells of a row by column name, each the canonical text of its value or null for an empty optional column. They are kept
// in the ordinal order of the names, so two rows with the same cells are equal whatever order their file listed them in.
internal sealed record ParameterCells
{
    private readonly KeyValuePair<string, string?>[] _cells;

    private readonly Dictionary<string, string?> _byColumn;

    private ParameterCells(KeyValuePair<string, string?>[] cells)
    {
        _cells = cells;
        _byColumn = cells.ToDictionary(cell => cell.Key, cell => cell.Value, StringComparer.Ordinal);
    }

    public IReadOnlyList<KeyValuePair<string, string?>> Values => _cells;

    public string? this[string column] =>
        _byColumn.TryGetValue(column, out var value) ? value : throw new KeyNotFoundException($"The row has no cell of column {column}.");

    public static ParameterCells Of(IEnumerable<KeyValuePair<string, string?>> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);

        var ordered = cells.OrderBy(cell => cell.Key, StringComparer.Ordinal).ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            if (string.IsNullOrEmpty(ordered[index].Key))
            {
                throw new ArgumentException("A cell names no column.", nameof(cells));
            }

            if (index > 0 && string.Equals(ordered[index - 1].Key, ordered[index].Key, StringComparison.Ordinal))
            {
                throw new ArgumentException($"The cells name column {ordered[index].Key} twice.", nameof(cells));
            }
        }

        return new ParameterCells(ordered);
    }

    public bool Holds(string column) => _byColumn.ContainsKey(column);

    public bool Equals(ParameterCells? other)
    {
        if (other is null || other._cells.Length != _cells.Length)
        {
            return false;
        }

        for (var index = 0; index < _cells.Length; index++)
        {
            if (!string.Equals(_cells[index].Key, other._cells[index].Key, StringComparison.Ordinal)
                || !string.Equals(_cells[index].Value, other._cells[index].Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var (column, value) in _cells)
        {
            hash.Add(column, StringComparer.Ordinal);
            hash.Add(value, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
