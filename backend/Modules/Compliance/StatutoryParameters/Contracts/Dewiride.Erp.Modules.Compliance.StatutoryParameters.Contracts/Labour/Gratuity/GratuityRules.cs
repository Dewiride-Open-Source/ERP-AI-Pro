namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Gratuity;

// A rule of the Code binds every establishment Chapter V of the Code on Social Security, 2020 covers, and a rule of the Social
// Security (Central) Rules, 2026 only one for which the Central Government is the appropriate Government (section 2(3)). The one
// year of section 2(o)(c) of the Industrial Relations Code, 2020 binds a fixed-term worker, as its section 2(zr) defines one; any
// other fixed-term employee has the Code's sections 2(34)(b) and 53 alone, with rule 33(1)(a)'s one year in the central sphere
// from 8 May 2026. No rate of interest on gratuity paid late is held, so a consumer computes none rather than read its absence as
// no interest due.
public sealed record GratuityRules(StatutoryParameterVersion Version, IReadOnlyList<GratuityRule> Rules) : IStatutoryParameterSet<GratuityRules>
{
    public static string Key => "gratuity.rules";

    public static GratuityRules Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<GratuityRule>());
}
