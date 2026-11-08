using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.AdvanceTax;

// A due date is DueDay of the month TaxYearMonth numbers within the tax year, April being 1 as FinancialPeriod numbers the months
// of a financial year, so 3 is June and 12 is March; CumulativePercent is the share of the year's advance tax due by then, earlier
// instalments counted, and 100 where the Act says the whole amount.
public sealed record AdvanceTaxInstalment(
    string Rule,
    string Name,
    AdvanceTaxPayers? Payers,
    int? DueDay,
    int? TaxYearMonth,
    Percentage? CumulativePercent,
    Money? Amount,
    string Wording);
