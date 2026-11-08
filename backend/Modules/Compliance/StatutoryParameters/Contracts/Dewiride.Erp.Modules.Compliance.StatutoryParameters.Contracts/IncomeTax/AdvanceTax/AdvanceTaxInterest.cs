namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.AdvanceTax;

public sealed record AdvanceTaxInterest(StatutoryParameterVersion Version, IReadOnlyList<AdvanceTaxInterestRule> Rules) : IStatutoryParameterSet<AdvanceTaxInterest>
{
    public static string Key => "income-tax.advance-tax-interest";

    public static AdvanceTaxInterest Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<AdvanceTaxInterestRule>());
}
