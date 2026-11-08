namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Rates;

public sealed record TdsRates(StatutoryParameterVersion Version, IReadOnlyList<TdsRate> Rates) : IStatutoryParameterSet<TdsRates>
{
    public static string Key => "income-tax.tds-rates";

    public static TdsRates Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<TdsRate>());
}
