namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.AdvanceTax;

public sealed record AdvanceTaxInstalments(StatutoryParameterVersion Version, IReadOnlyList<AdvanceTaxInstalment> Rules) : IStatutoryParameterSet<AdvanceTaxInstalments>
{
    public static string Key => "income-tax.advance-tax-instalments";

    public static AdvanceTaxInstalments Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<AdvanceTaxInstalment>());
}
