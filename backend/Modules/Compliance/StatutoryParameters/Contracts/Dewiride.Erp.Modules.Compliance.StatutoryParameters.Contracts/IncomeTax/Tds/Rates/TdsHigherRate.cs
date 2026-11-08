using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Rates;

// Name is the ERP's title of the case and Words the provision as the Act or the Rules print it; Percent is the rate the case
// names, null where it names none.
public sealed record TdsHigherRate(string Case, string Name, string? Entries, TdsHigherRateEffect Effect, Percentage? Percent, string Words);
