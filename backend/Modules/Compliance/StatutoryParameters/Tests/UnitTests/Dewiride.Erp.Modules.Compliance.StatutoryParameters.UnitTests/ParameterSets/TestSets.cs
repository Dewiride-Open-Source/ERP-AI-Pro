using System.Globalization;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.UnitTests.ParameterSets;

// Probe sets the tests build: no statutory value, only the shapes the rules are about.
internal static class TestSets
{
    public static ParameterColumn Column(
        string name,
        CellKind kind,
        bool isKey = false,
        bool isRowTitle = false,
        bool isOptional = false,
        decimal? minimum = null,
        decimal? maximum = null,
        IEnumerable<ParameterChoice>? choices = null) =>
        new(name, $"Column {name}", kind, isKey, isRowTitle, isOptional, minimum, maximum, choices ?? []);

    public static ParameterSet Set(params ParameterColumn[] columns) =>
        ParameterSet.Create("test.probe-set", "Probe set", "A set the tests build.", SetBasis.Law, AsOfDate.SupplyDate, ParameterColumnSet.Create(columns));

    public static ParameterSet RateSet() =>
        Set(Column("entry", CellKind.Code, isKey: true), Column("rate", CellKind.Percent), Column("note", CellKind.Text, isOptional: true));

    public static DateOnly Day(string date) => DateOnly.Parse(date, CultureInfo.InvariantCulture);

    public static ParameterCells Cells(params (string Column, string? Value)[] cells) =>
        ParameterCells.Of(cells.Select(cell => KeyValuePair.Create(cell.Column, cell.Value)));

    public static ParameterRow Rate(ParameterSet set, string entry, string from, string? until, string rate = "9", string? note = null) =>
        ParameterRow.Create(set, Day(from), until is null ? null : Day(until), Cells(("entry", entry), ("rate", rate), ("note", note)), $"Probe source of {entry}");
}
