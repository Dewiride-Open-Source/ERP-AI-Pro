namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Bonus;

// The ERP's reading, not a government table: the limits of the repealed Payment of Bonus Act, 1965 that section 69(2) of the Code
// on Wages, 2019 keeps in force for a State or Union territory whose own notification under section 26 is not held. A State's row
// of BonusWageLimits replaces its reading, and a State with neither, Bihar among them, has no limits a consumer may apply.
public sealed record StateWageLimitReadings(StatutoryParameterVersion Version, IReadOnlyList<StateWageLimitReading> Readings) : IStatutoryParameterSet<StateWageLimitReadings>
{
    public static string Key => "bonus.state-wage-limit-readings";

    public static StateWageLimitReadings Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<StateWageLimitReading>());
}
