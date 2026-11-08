namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Company;

// The cess is the Finance Act's for one tax year, so a version without its rows means that year's Finance Act is not yet seeded,
// and a consumer refuses to compute rather than read an empty answer as no cess due.
public sealed record HealthAndEducationCess(StatutoryParameterVersion Version, IReadOnlyList<CessRate> Rates) : IStatutoryParameterSet<HealthAndEducationCess>
{
    public static string Key => "income-tax.health-and-education-cess";

    public static HealthAndEducationCess Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<CessRate>());
}
