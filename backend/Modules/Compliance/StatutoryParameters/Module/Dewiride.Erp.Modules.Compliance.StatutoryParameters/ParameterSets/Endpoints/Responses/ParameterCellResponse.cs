namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Responses;

/// <summary>One cell of an entry.</summary>
/// <param name="Column">Name of the cell's column.</param>
/// <param name="Value">The value as text: a number without trailing zeros, such as 2.5 or 50000000, a date as yyyy-MM-dd, and a code, a choice's value, a GST State code or a text as written; null when an optional column is empty.</param>
/// <param name="StateName">The name of the State or Union territory that a GST State code cell names on the entry's first day; null for every other cell.</param>
internal sealed record ParameterCellResponse(string Column, string? Value, string? StateName);
