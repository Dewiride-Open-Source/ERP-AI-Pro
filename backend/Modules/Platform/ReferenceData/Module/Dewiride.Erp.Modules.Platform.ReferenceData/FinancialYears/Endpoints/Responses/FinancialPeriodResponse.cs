namespace Dewiride.Erp.Modules.Platform.ReferenceData.FinancialYears.Endpoints.Responses;

/// <summary>One monthly period of a financial year.</summary>
/// <param name="Number">Number of the period, 1 to 12, April being 1.</param>
/// <param name="Label">The month and its calendar year, such as April 2026.</param>
/// <param name="Quarter">Number of the quarter holding the period, 1 to 4.</param>
/// <param name="Start">First day of the month.</param>
/// <param name="End">Last day of the month.</param>
internal sealed record FinancialPeriodResponse(int Number, string Label, int Quarter, DateOnly Start, DateOnly End);
