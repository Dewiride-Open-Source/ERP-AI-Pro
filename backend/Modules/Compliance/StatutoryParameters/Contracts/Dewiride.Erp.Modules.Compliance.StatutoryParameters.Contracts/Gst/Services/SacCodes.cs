namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Services;

public sealed record SacCodes(StatutoryParameterVersion Version, IReadOnlyList<SacCode> Codes) : IStatutoryParameterSet<SacCodes>
{
    public static string Key => "gst.sac-codes";

    public static SacCodes Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<SacCode>());
}
