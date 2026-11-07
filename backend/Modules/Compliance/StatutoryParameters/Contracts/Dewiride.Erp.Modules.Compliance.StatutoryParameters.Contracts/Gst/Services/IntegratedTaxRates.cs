namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Services;

public sealed record IntegratedTaxRates(StatutoryParameterVersion Version, IReadOnlyList<ServiceRateEntry> Entries) : IStatutoryParameterSet<IntegratedTaxRates>
{
    public static string Key => "gst.integrated-tax-rates";

    public static IntegratedTaxRates Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<ServiceRateEntry>());
}
