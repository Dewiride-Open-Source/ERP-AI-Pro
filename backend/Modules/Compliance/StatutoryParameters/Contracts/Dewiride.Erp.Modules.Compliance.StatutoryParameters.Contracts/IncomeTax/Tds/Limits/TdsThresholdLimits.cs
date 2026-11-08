namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Limits;

public sealed record TdsThresholdLimits(StatutoryParameterVersion Version, IReadOnlyList<TdsThresholdLimit> Limits) : IStatutoryParameterSet<TdsThresholdLimits>
{
    public static string Key => "income-tax.tds-threshold-limits";

    public static TdsThresholdLimits Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<TdsThresholdLimit>());
}
