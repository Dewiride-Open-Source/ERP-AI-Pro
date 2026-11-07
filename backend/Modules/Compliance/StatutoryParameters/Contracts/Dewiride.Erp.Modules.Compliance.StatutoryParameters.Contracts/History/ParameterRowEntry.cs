namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;

public sealed record ParameterRowEntry(string Key, DateOnly From, DateOnly? Until, string Source, IReadOnlyList<ParameterCellEntry> Cells);
