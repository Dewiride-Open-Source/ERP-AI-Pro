namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Esi;

// The rules of the Employees' State Insurance (General) Regulations, 1950 end on 20 November 2026, the earlier day on which the one
// year that section 164(2)(b) of the Code on Social Security, 2020 keeps them in force may end, while the Code's own rule runs on,
// so a version without a payment rule means none is seeded, never that no payment is due.
public sealed record EsiContributionDueDates(StatutoryParameterVersion Version, IReadOnlyList<EsiContributionDueDate> Rules) : IStatutoryParameterSet<EsiContributionDueDates>
{
    public static string Key => "esi.contribution-due-dates";

    public static EsiContributionDueDates Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<EsiContributionDueDate>());
}
