namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Entries;

public sealed record TdsEntries(StatutoryParameterVersion Version, IReadOnlyList<TdsEntry> Entries) : IStatutoryParameterSet<TdsEntries>
{
    public static string Key => "income-tax.tds-entries";

    public static TdsEntries Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<TdsEntry>());
}
