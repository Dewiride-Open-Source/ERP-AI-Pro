namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Returns;

public sealed record GstReturnThresholds(StatutoryParameterVersion Version, IReadOnlyList<GstReturnThreshold> Thresholds) : IStatutoryParameterSet<GstReturnThresholds>
{
    public static string Key => "gst.return-thresholds";

    public static GstReturnThresholds Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<GstReturnThreshold>());
}
