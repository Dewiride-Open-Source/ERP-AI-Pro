namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Rates;

public sealed record TdsHigherRates(StatutoryParameterVersion Version, IReadOnlyList<TdsHigherRate> Cases) : IStatutoryParameterSet<TdsHigherRates>
{
    public static string Key => "income-tax.tds-higher-rates";

    public static TdsHigherRates Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<TdsHigherRate>());
}
