using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Esi;

// A share is Rate of the wages payable for the wage period, rounded up to a multiple of RoundedUpTo, which is one rupee for the
// rule's "rounded to the next higher rupee"; each share is rounded on its own.
public sealed record EsiContributionRate(string Share, string Name, Percentage Rate, Money RoundedUpTo, string Wording);
