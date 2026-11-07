namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;

// A cell holds its value as canonical text whatever the column's kind: a number without trailing zeros, such as "2.5" or
// "50000000", a code, a choice's value or a GST State code as written, and null when an optional column is empty.
public sealed record ParameterCellEntry(string Column, string? Value);
