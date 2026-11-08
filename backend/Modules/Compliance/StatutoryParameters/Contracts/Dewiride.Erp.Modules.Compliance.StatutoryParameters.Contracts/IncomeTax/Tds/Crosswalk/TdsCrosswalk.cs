namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Crosswalk;

// The ERP's reading, not a government table: the provision of the Income-tax Act, 2025 that governs a payment on which tax was
// deducted under a provision of the Income-tax Act, 1961, or none.
public sealed record TdsCrosswalk(StatutoryParameterVersion Version, IReadOnlyList<TdsCrosswalkPair> Pairs) : IStatutoryParameterSet<TdsCrosswalk>
{
    public static string Key => "income-tax.tds-crosswalk";

    public static TdsCrosswalk Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<TdsCrosswalkPair>());
}
