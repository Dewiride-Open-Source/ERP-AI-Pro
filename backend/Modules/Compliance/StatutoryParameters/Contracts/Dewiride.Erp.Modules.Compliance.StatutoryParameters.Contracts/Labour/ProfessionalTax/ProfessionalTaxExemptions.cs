namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProfessionalTax;

// A class without a row says nothing of whether it is exempt: the set holds only the exemptions of the instruments the ERP has read.
public sealed record ProfessionalTaxExemptions(StatutoryParameterVersion Version, IReadOnlyList<ProfessionalTaxExemption> Exemptions) : IStatutoryParameterSet<ProfessionalTaxExemptions>
{
    public static string Key => "professional-tax.exemptions";

    public static ProfessionalTaxExemptions Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<ProfessionalTaxExemption>());
}
