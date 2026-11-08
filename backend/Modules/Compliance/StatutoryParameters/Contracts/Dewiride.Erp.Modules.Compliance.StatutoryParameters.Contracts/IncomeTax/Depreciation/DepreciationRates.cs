namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Depreciation;

public sealed record DepreciationRates(StatutoryParameterVersion Version, IReadOnlyList<DepreciationRate> Rates) : IStatutoryParameterSet<DepreciationRates>
{
    public static string Key => "income-tax.depreciation-rates";

    public static DepreciationRates Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<DepreciationRate>());
}
