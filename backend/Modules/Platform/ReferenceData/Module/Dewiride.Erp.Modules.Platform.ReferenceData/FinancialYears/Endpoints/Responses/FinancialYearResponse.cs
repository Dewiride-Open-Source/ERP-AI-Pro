namespace Dewiride.Erp.Modules.Platform.ReferenceData.FinancialYears.Endpoints.Responses;

/// <summary>One financial year with its quarters and its monthly periods.</summary>
/// <param name="Label">Label of the year, such as 2026-27.</param>
/// <param name="Start">1 April that starts the year.</param>
/// <param name="End">31 March that ends the year.</param>
/// <param name="Quarters">The four quarters, the first starting in April.</param>
/// <param name="Periods">The twelve monthly periods, April being the first.</param>
internal sealed record FinancialYearResponse(
    string Label,
    DateOnly Start,
    DateOnly End,
    IReadOnlyList<FinancialQuarterResponse> Quarters,
    IReadOnlyList<FinancialPeriodResponse> Periods);
