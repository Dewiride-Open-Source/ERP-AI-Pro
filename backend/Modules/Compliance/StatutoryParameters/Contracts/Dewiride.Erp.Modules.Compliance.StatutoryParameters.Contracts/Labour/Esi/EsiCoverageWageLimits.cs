namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Esi;

// The limits end on 7 May 2026 with the Rules that held them, and no wage ceiling for Chapter IV of the Code on Social Security,
// 2020 has been read in an instrument, so a version without a limit means none is seeded, never that every employee is covered or
// that none is.
public sealed record EsiCoverageWageLimits(StatutoryParameterVersion Version, IReadOnlyList<EsiCoverageWageLimit> Limits) : IStatutoryParameterSet<EsiCoverageWageLimits>
{
    public static string Key => "esi.coverage-wage-limits";

    public static EsiCoverageWageLimits Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<EsiCoverageWageLimit>());
}
