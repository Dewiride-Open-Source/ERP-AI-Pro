namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Responses;

/// <summary>One entry of a set over the dates it is in force.</summary>
/// <param name="Key">The entry's key, the value of the set's key column.</param>
/// <param name="From">First date the entry is in force.</param>
/// <param name="Until">Last date the entry is in force; null while it still is.</param>
/// <param name="Source">The clause of the instrument that gave the entry its text and values.</param>
/// <param name="Cells">The entry's cells, one per column in the order of the set's columns.</param>
internal sealed record ParameterRowResponse(string Key, DateOnly From, DateOnly? Until, string Source, IReadOnlyList<ParameterCellResponse> Cells);
