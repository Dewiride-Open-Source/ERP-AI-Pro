namespace Dewiride.Erp.Modules.Platform.ReferenceData.FinancialYears.Endpoints.Responses;

/// <summary>One quarter of a financial year.</summary>
/// <param name="Number">Number of the quarter, 1 to 4, the first starting in April.</param>
/// <param name="Label">Label of the quarter, such as Q1 2026-27.</param>
/// <param name="Start">First day of the quarter.</param>
/// <param name="End">Last day of the quarter.</param>
internal sealed record FinancialQuarterResponse(int Number, string Label, DateOnly Start, DateOnly End);
