namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Company;

// Every band is the Finance Act's for one tax year, so a version without bands means that year's Finance Act is not yet seeded, and
// a consumer refuses to compute the surcharge rather than read an empty answer as nothing due.
public sealed record CompanySurcharges(StatutoryParameterVersion Version, IReadOnlyList<CompanySurcharge> Bands) : IStatutoryParameterSet<CompanySurcharges>
{
    public static string Key => "income-tax.company-surcharge";

    public static CompanySurcharges Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<CompanySurcharge>());
}
