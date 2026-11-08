namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Esi;

// The rates start with rule 19 of the Social Security (Central) Rules, 2026 on 8 May 2026, so a wage period that ends earlier reads
// no set, and a consumer refuses to compute rather than read the absence as no contribution due.
public sealed record EsiContributionRates(StatutoryParameterVersion Version, IReadOnlyList<EsiContributionRate> Rates) : IStatutoryParameterSet<EsiContributionRates>
{
    public static string Key => "esi.contribution-rates";

    public static EsiContributionRates Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<EsiContributionRate>());
}
