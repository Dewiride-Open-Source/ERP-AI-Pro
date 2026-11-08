using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProvidentFund.Remittance;

// Each contribution is calculated to the nearest rupee: a fraction of a rupee of NextRupeeFrom or more counts as the next higher
// rupee, and a smaller fraction is ignored.
public sealed record ContributionRounding(string Contributions, string Name, Money NextRupeeFrom, string Wording);
