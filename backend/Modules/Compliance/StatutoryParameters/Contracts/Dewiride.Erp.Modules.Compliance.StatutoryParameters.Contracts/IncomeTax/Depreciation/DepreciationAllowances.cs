namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Depreciation;

public sealed record DepreciationAllowances(StatutoryParameterVersion Version, IReadOnlyList<DepreciationAllowance> Allowances) : IStatutoryParameterSet<DepreciationAllowances>
{
    public static string Key => "income-tax.depreciation-allowances";

    public static DepreciationAllowances Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<DepreciationAllowance>());
}
