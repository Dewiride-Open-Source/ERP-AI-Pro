using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProvidentFund.Contributions;

// Rate is a per cent of a month's wages, counted up to WagesUpTo when it is set and applicable to wages above WagesAbove when that
// is set; it is null when Share makes the contribution equal to the employer's, and a contribution that is part of the employer's is
// paid out of it, never besides it.
public sealed record ProvidentFundContributionRate(
    string Contribution,
    string Name,
    ContributionPayer Payer,
    ContributionFund Fund,
    ContributionShare Share,
    Percentage? Rate,
    Money? WagesUpTo,
    Money? WagesAbove,
    string Wording);
