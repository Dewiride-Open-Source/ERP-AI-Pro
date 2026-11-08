using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Depreciation;

// Percent is of what Test names: of the prescribed rate for the half rate, of the actual cost for additional depreciation, and
// of the written down value for the caps and the laboratory-technology rate. Days is the number of days of use in the tax year
// below which the allowance applies, null where its test names none.
public sealed record DepreciationAllowance(string Allowance, string Name, Percentage Percent, int? Days, string Test);
