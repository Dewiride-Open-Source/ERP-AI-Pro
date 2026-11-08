namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.FinanceAct;

// The ERP's reading, not a government table: the Part II item whose rate an entry deducted at the rates in force takes for a
// payee, where the entry's nature of income fixes it. The readings end with the rates they read, so a version in force that holds
// none means that year's Finance Act is not yet seeded, and a consumer refuses to compute rather than read the empty list as
// nothing to deduct.
public sealed record TdsRateInForceReadings(StatutoryParameterVersion Version, IReadOnlyList<TdsRateInForceReading> Readings) : IStatutoryParameterSet<TdsRateInForceReadings>
{
    public static string Key => "income-tax.tds-rates-in-force-readings";

    public static TdsRateInForceReadings Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<TdsRateInForceReading>());
}
