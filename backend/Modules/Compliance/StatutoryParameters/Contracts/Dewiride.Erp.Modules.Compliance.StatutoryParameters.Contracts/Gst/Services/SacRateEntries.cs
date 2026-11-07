namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Services;

// The ERP's reading, not a government table: the rate entry each code's services fall under by default. An entry that applies
// by what a particular supply is belongs to no code.
public sealed record SacRateEntries(StatutoryParameterVersion Version, IReadOnlyList<SacRateEntry> Entries) : IStatutoryParameterSet<SacRateEntries>
{
    public static string Key => "gst.sac-rate-entries";

    public static SacRateEntries Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<SacRateEntry>());
}
