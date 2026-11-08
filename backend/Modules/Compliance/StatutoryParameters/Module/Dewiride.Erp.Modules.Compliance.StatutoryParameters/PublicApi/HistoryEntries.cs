using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Timeline;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.PublicApi;

// A row's cells are given in the order the set declares its columns.
internal static class HistoryEntries
{
    public static ParameterRowEntry RowOf(ParameterSet set, ParameterRow row) =>
        new(row.RowKey, row.From, row.Until, row.Source, [.. set.Columns.Columns.Select(column => new ParameterCellEntry(column.Name, row.Cells[column.Name]))]);

    public static ParameterVersionEntry VersionOf(ParameterVersion version, ParameterTimeline timeline)
    {
        var changes = timeline.ChangesAt(version.From);

        return new ParameterVersionEntry(version.From, timeline.UntilOf(version.From), version.Instruments, changes.Added.Count, changes.Changed.Count, changes.Removed.Count);
    }

    public static ParameterColumnEntry ColumnOf(ParameterColumn column) =>
        new(
            column.Name,
            column.Title,
            KindOf(column.Kind),
            column.IsKey,
            column.IsRowTitle,
            column.IsOptional,
            [.. column.Choices.Select(choice => new ParameterChoiceEntry(choice.Value, choice.Title))]);

    public static ParameterSetBasis BasisOf(SetBasis basis) => basis switch
    {
        SetBasis.Law => ParameterSetBasis.Law,
        SetBasis.Interpretation => ParameterSetBasis.Interpretation,
        _ => throw new ArgumentOutOfRangeException(nameof(basis), basis, "A set is the law's or the ERP's reading of it."),
    };

    public static ParameterAsOf AsOfOf(AsOfDate asOf) => asOf switch
    {
        AsOfDate.SupplyDate => ParameterAsOf.SupplyDate,
        AsOfDate.TaxPeriodStart => ParameterAsOf.TaxPeriodStart,
        AsOfDate.DeductionDate => ParameterAsOf.DeductionDate,
        AsOfDate.FinancialYearStart => ParameterAsOf.FinancialYearStart,
        _ => throw new ArgumentOutOfRangeException(nameof(asOf), asOf, "A set answers for one of the store's as-of dates."),
    };

    private static ParameterColumnKind KindOf(CellKind kind) => kind switch
    {
        CellKind.Text => ParameterColumnKind.Text,
        CellKind.Code => ParameterColumnKind.Code,
        CellKind.Integer => ParameterColumnKind.Integer,
        CellKind.Percent => ParameterColumnKind.Percent,
        CellKind.Amount => ParameterColumnKind.Amount,
        CellKind.Choice => ParameterColumnKind.Choice,
        CellKind.StateCode => ParameterColumnKind.StateCode,
        CellKind.Date => ParameterColumnKind.Date,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A column is of one of the store's kinds."),
    };
}
