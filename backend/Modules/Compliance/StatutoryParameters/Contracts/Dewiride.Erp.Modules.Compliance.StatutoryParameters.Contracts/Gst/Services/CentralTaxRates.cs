namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Services;

public sealed record CentralTaxRates(StatutoryParameterVersion Version, IReadOnlyList<ServiceRateEntry> Entries) : IStatutoryParameterSet<CentralTaxRates>
{
    public static string Key => "gst.central-tax-rates";

    public static CentralTaxRates Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<ServiceRateEntry>());
}
