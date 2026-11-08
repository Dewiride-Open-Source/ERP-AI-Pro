namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Esi;

// The exemption of the employee's contribution ends on 7 May 2026 with the Rules that held it, and section 2(31) of the Code on
// Social Security, 2020 needs a notification of the wage that no instrument read gives, so a version without it means none is
// seeded, never that no employee is exempt.
public sealed record EsiContributionExemptions(StatutoryParameterVersion Version, IReadOnlyList<EsiContributionExemption> Exemptions) : IStatutoryParameterSet<EsiContributionExemptions>
{
    public static string Key => "esi.contribution-exemptions";

    public static EsiContributionExemptions Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<EsiContributionExemption>());
}
