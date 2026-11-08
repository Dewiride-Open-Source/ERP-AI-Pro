using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.AdvanceTax;

// A due date is DueDay of the month TaxYearMonth numbers within the tax year, April being 1; interest arises when the advance tax
// paid by then is less than Share of ShareOf, unless it reaches NoInterestAtOrAbove of it, and Interest is charged on the shortfall
// once or for every month or part of a month, as InterestBasis says.
public sealed record AdvanceTaxInterestRule(
    string Rule,
    string Name,
    int? DueDay,
    int? TaxYearMonth,
    Percentage? Share,
    AdvanceTaxShareOf? ShareOf,
    Percentage? NoInterestAtOrAbove,
    Percentage? Interest,
    AdvanceTaxInterestBasis? InterestBasis,
    string Wording);
