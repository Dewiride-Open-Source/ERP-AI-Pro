namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProfessionalTax;

// A State that levies the tax but has no slab on a date is one whose Schedule for that date the ERP has not read, so a consumer
// refuses to compute rather than read no slab as no tax due.
public sealed record ProfessionalTaxSlabs(StatutoryParameterVersion Version, IReadOnlyList<ProfessionalTaxSlab> Slabs) : IStatutoryParameterSet<ProfessionalTaxSlabs>
{
    public static string Key => "professional-tax.salary-slabs";

    public static ProfessionalTaxSlabs Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<ProfessionalTaxSlab>());
}
