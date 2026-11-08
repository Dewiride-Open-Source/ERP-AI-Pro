namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Company;

public sealed record MinimumAlternateTax(StatutoryParameterVersion Version, IReadOnlyList<MinimumAlternateTaxRule> Rules) : IStatutoryParameterSet<MinimumAlternateTax>
{
    public static string Key => "income-tax.minimum-alternate-tax";

    public static MinimumAlternateTax Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<MinimumAlternateTaxRule>());
}
