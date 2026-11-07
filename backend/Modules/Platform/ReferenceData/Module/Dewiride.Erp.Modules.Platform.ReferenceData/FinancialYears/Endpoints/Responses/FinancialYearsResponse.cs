namespace Dewiride.Erp.Modules.Platform.ReferenceData.FinancialYears.Endpoints.Responses;

/// <summary>Financial years of India, each from 1 April to 31 March.</summary>
/// <param name="Years">The financial years asked for, the earliest first.</param>
internal sealed record FinancialYearsResponse(IReadOnlyList<FinancialYearResponse> Years);
