namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;

// A cell holds its value as canonical text whatever the column's kind: a number without trailing zeros, such as "2.5", "0.75" or
// "50000000", a code, a choice's value or a GST State code as written, a date as yyyy-MM-dd, a day and month as MM-dd, and null
// when an optional column is empty.
public sealed record ParameterCellEntry(string Column, string? Value);
