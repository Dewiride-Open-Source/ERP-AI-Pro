namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Companies.ScheduleII;

// Part C's continuous process plant, item IV(i)(b), has no row: its life from 1 April 2014 was set by a notification whose
// Gazette copy has not been read, so no useful life follows from this set for it.
public sealed record UsefulLives(StatutoryParameterVersion Version, IReadOnlyList<UsefulLife> Lives) : IStatutoryParameterSet<UsefulLives>
{
    public static string Key => "companies.schedule-ii-useful-lives";

    public static UsefulLives Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<UsefulLife>());
}
