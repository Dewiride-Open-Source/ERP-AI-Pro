using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Company;

// A band covers a total income that exceeds IncomeAbove and does not exceed IncomeUpTo, a null bound being none.
public sealed record CompanySurcharge(
    string Band,
    string Name,
    CompanySurchargeTaxOn TaxOn,
    Percentage Rate,
    Money? IncomeAbove,
    Money? IncomeUpTo,
    string Persons,
    string Wording,
    string? MarginalRelief);
