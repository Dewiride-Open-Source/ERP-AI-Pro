namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProvidentFund.Contributions;

// A contribution without an entry on a day is one whose instrument for that day is not seeded, never one that is not payable, so a
// consumer refuses to compute it rather than read the gap as nothing due.
public sealed record ProvidentFundContributionRates(StatutoryParameterVersion Version, IReadOnlyList<ProvidentFundContributionRate> Rates) : IStatutoryParameterSet<ProvidentFundContributionRates>
{
    public static string Key => "provident-fund.contribution-rates";

    public static ProvidentFundContributionRates Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<ProvidentFundContributionRate>());
}
