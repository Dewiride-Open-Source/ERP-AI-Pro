using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Timeline;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;

// An entry of a set over the dates it is in force, known with its set by its key, the value of the set's key column, and the
// date it starts; a corrected start is another row. The source names the clause that gave the row its current text and value.
internal sealed class ParameterRow : AggregateRoot<ParameterRowId>, IAuditable
{
    public const int SourceMaxLength = 400;

    private ParameterRow(ParameterRowId id)
        : base(id)
    {
    }

    public ParameterSetId SetId { get; private set; }

    public string RowKey { get; private set; } = string.Empty;

    public DateOnly From { get; private set; }

    public DateOnly? Until { get; private set; }

    public ParameterCells Cells { get; private set; } = null!;

    public string Source { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public DateSpan Span => new(From, Until);

    public static ParameterRow Create(ParameterSet set, DateOnly from, DateOnly? until, ParameterCells cells, string source)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(cells);

        var key = cells.Holds(set.Columns.KeyColumn.Name) ? cells[set.Columns.KeyColumn.Name] : null;
        if (key is null)
        {
            throw new ArgumentException($"The row from {from:yyyy-MM-dd} has no value in key column {set.Columns.KeyColumn.Name}.", nameof(cells));
        }

        var row = new ParameterRow(ParameterRowId.Create()) { SetId = set.Id, RowKey = key, From = from };
        row.Revise(set, until, cells, source);

        return row;
    }

    public void Revise(ParameterSet set, DateOnly? until, ParameterCells cells, string source)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(cells);

        if (set.Id != SetId)
        {
            throw new ArgumentException($"Row {RowKey} from {From:yyyy-MM-dd} belongs to another set than {set.Key}.", nameof(set));
        }

        if (until < From)
        {
            throw new ArgumentOutOfRangeException(nameof(until), until, $"Row {RowKey} from {From:yyyy-MM-dd} cannot end before it starts.");
        }

        if (set.Columns.ProblemsOf(cells) is [var problem, ..])
        {
            throw new ArgumentException($"Row {RowKey} from {From:yyyy-MM-dd}: {problem}.", nameof(cells));
        }

        if (!string.Equals(cells[set.Columns.KeyColumn.Name], RowKey, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Row {RowKey} from {From:yyyy-MM-dd} keeps its key; a row of another key is another row.", nameof(cells));
        }

        Until = until;
        Cells = cells;
        Source = ParameterText.Require(source, SourceMaxLength, $"The source of row {RowKey} from {From:yyyy-MM-dd}");
    }
}
