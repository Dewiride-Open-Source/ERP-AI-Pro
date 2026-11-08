using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Company;

// A rate with a TurnoverLimit applies to a company whose total turnover or gross receipt in TurnoverYear does not exceed it.
public sealed record CompanyTaxRate(
    string Entry,
    string Name,
    CompanyTaxRegime Regime,
    Percentage Rate,
    Money? TurnoverLimit,
    string? TurnoverYear,
    DateOnly? SetUpOnOrAfter,
    DateOnly? ProductionBy,
    string Wording,
    string? Option,
    string? ExcludedBusinesses);
