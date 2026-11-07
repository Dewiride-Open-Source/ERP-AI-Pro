namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;

public sealed record ParameterVersionView(
    DateOnly From,
    DateOnly? Until,
    IReadOnlyList<string> Instruments,
    IReadOnlyList<ParameterRowEntry> Rows,
    IReadOnlyList<ParameterRowEntry> Added,
    IReadOnlyList<ParameterRowChange> Changed,
    IReadOnlyList<ParameterRowEntry> Removed);
