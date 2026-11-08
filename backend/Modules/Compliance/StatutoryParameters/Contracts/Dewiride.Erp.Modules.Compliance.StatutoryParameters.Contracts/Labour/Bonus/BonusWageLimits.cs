namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Bonus;

// A row is the limits one appropriate Government determined by notification under section 26 of the Code on Wages, 2019: the
// Central Government's, keyed central, for the establishments of section 2(d)(i), and a State's or Union territory's, keyed by its
// GST State code, which a consumer reads before that State's row of StateWageLimitReadings. Minimum wages are not held, so the
// calculation wage is its amount or the minimum wage its wording names, whichever is higher.
public sealed record BonusWageLimits(StatutoryParameterVersion Version, IReadOnlyList<BonusWageLimit> Limits) : IStatutoryParameterSet<BonusWageLimits>
{
    public static string Key => "bonus.wage-limits";

    public static BonusWageLimits Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<BonusWageLimit>());
}
