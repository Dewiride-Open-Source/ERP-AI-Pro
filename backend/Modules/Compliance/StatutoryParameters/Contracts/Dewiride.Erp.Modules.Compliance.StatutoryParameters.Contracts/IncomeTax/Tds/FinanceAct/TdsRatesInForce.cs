namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.FinanceAct;

// The rates of one Finance Act's Part II, which end with the tax year it gives them for. A version in force that holds no rate
// means that year's Finance Act is not yet seeded, so a consumer refuses to compute the deduction rather than read the empty list
// as nothing to deduct.
public sealed record TdsRatesInForce(StatutoryParameterVersion Version, IReadOnlyList<TdsRateInForce> Rates) : IStatutoryParameterSet<TdsRatesInForce>
{
    public static string Key => "income-tax.tds-rates-in-force";

    public static TdsRatesInForce Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<TdsRateInForce>());
}
