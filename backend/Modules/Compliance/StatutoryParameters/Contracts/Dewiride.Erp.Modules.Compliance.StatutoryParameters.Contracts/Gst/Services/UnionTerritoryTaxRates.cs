namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Services;

public sealed record UnionTerritoryTaxRates(StatutoryParameterVersion Version, IReadOnlyList<ServiceRateEntry> Entries) : IStatutoryParameterSet<UnionTerritoryTaxRates>
{
    public static string Key => "gst.union-territory-tax-rates";

    public static UnionTerritoryTaxRates Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<ServiceRateEntry>());
}
