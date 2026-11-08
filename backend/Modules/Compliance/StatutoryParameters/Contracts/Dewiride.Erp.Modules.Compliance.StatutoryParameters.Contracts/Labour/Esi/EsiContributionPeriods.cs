namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Esi;

// The periods are those of the Employees' State Insurance (General) Regulations, 1950, which section 164(2)(b) of the Code on
// Social Security, 2020 keeps in force for one year from 21 November 2025; their rows end on 20 November 2026, the earlier day on
// which that year may end, so a version without them means none is seeded, never that no period applies.
public sealed record EsiContributionPeriods(StatutoryParameterVersion Version, IReadOnlyList<EsiContributionPeriod> Periods) : IStatutoryParameterSet<EsiContributionPeriods>
{
    public static string Key => "esi.contribution-periods";

    public static EsiContributionPeriods Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<EsiContributionPeriod>());
}
