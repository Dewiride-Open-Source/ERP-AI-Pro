using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Rates;

// Percent is the percentage a fixed rate states, null for the rates in force, an average rate, a deduction under Schedule XI and
// an agreement's rate. BaseWords null means the rate applies to what its sub-section names: the entire amount of the income or sum
// (section 393(1)(a) and 393(3)(a)) or the amount of the income or sum (section 393(2)).
public sealed record TdsRate(string Rate, string Entry, string? Case, string Words, TdsRateBasis Basis, Percentage? Percent, string? BaseWords);
