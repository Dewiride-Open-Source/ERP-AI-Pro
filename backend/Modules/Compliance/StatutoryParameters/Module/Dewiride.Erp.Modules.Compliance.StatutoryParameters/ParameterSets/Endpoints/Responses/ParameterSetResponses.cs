using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Application.Queries.GetParameterSet;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Responses;

internal static class ParameterSetResponses
{
    public static ParameterSetsResponse Of(IReadOnlyList<ParameterSetEntry> sets) =>
        new([.. sets.Select(set => new ParameterSetSummaryResponse(
            set.Key,
            set.Title,
            set.Description,
            set.Basis,
            set.AsOf,
            set.FirstVersionFrom,
            set.VersionCount,
            set.Current is null ? null : VersionOf(set.Current),
            set.Next is null ? null : VersionOf(set.Next),
            set.EntryCount))]);

    public static ParameterSetResponse Of(ShownParameterSet shownSet)
    {
        var (set, shown, names) = shownSet;
        var stateColumns = set.Columns.Where(column => column.Kind == ParameterColumnKind.StateCode).Select(column => column.Name).ToHashSet(StringComparer.Ordinal);

        ParameterRowResponse RowOf(ParameterRowEntry row) =>
            new(row.Key, row.From, row.Until, row.Source, [.. row.Cells.Select(cell => new ParameterCellResponse(
                cell.Column,
                cell.Value,
                cell.Value is { } code && stateColumns.Contains(cell.Column) ? names.NameOf(code, row.From) : null))]);

        return new(
            set.Key,
            set.Title,
            set.Description,
            set.Basis,
            set.AsOf,
            [.. set.Columns.Select(column => new ParameterColumnResponse(
                column.Name,
                column.Title,
                column.Kind,
                column.IsKey,
                column.IsRowTitle,
                column.IsOptional,
                [.. column.Choices.Select(choice => new ParameterChoiceResponse(choice.Value, choice.Title))]))],
            [.. set.Versions.Select(VersionOf)],
            set.CurrentFrom,
            shown is null
                ? null
                : new ParameterVersionResponse(
                    shown.From,
                    shown.Until,
                    shown.Instruments,
                    [.. shown.Rows.Select(RowOf)],
                    [.. shown.Added.Select(RowOf)],
                    [.. shown.Changed.Select(change => new ParameterRowChangeResponse(RowOf(change.Before), RowOf(change.After)))],
                    [.. shown.Removed.Select(RowOf)]));
    }

    private static ParameterVersionSummaryResponse VersionOf(ParameterVersionEntry version) =>
        new(version.From, version.Until, version.Instruments, version.AddedCount, version.ChangedCount, version.RemovedCount);
}
