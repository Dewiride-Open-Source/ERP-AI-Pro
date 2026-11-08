using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Company;

// Percent is a rate on book profit or the share of the tax payable that a credit carried forward may set off, as the rule says;
// TaxYears counts the tax years after the one in which a credit first became allowable, the last of them included.
public sealed record MinimumAlternateTaxRule(string Rule, string Name, Percentage? Percent, int? TaxYears, string Wording);
