using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProvidentFund.Contributions;

// Amount is a month's wage or pay: the wage ceiling itself, or the most an employee may draw and still not be excluded from the
// Fund or still be admitted to the Pension Scheme, as Test says.
public sealed record ProvidentFundMembershipLimit(string Limit, string Name, Money Amount, MembershipTest Test, string Wording);
