namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Bonus;

// Every rule binds every establishment Chapter IV of the Code on Wages, 2019 covers, whatever its appropriate Government; the wage
// limits of section 26(1) and (2) are each appropriate Government's, read from BonusWageLimits and StateWageLimitReadings.
public sealed record BonusRules(StatutoryParameterVersion Version, IReadOnlyList<BonusRule> Rules) : IStatutoryParameterSet<BonusRules>
{
    public static string Key => "bonus.rules";

    public static BonusRules Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<BonusRule>());
}
