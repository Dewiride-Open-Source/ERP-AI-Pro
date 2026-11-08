namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProfessionalTax;

// A State without a row is one whose instrument the ERP has not read, which says nothing of whether it levies the tax: only a row
// says that none is levied, in the words of the text that says so.
public sealed record ProfessionalTaxLevies(StatutoryParameterVersion Version, IReadOnlyList<ProfessionalTaxLevy> States) : IStatutoryParameterSet<ProfessionalTaxLevies>
{
    public static string Key => "professional-tax.levies";

    public static ProfessionalTaxLevies Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<ProfessionalTaxLevy>());
}
