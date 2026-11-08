namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Company;

// The Finance Act's rates hold for one tax year, so a version without them means that year's Finance Act is not yet seeded, and a
// consumer refuses to compute the tax rather than read the missing rates as none; the options of sections 199 to 201 are standing
// law and stay in force.
public sealed record CompanyTaxRates(StatutoryParameterVersion Version, IReadOnlyList<CompanyTaxRate> Rates) : IStatutoryParameterSet<CompanyTaxRates>
{
    public static string Key => "income-tax.company-rates";

    public static CompanyTaxRates Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<CompanyTaxRate>());
}
