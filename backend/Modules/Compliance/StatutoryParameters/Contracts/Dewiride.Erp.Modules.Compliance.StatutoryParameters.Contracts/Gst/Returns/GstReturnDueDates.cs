namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Returns;

public sealed record GstReturnDueDates(StatutoryParameterVersion Version, IReadOnlyList<GstReturnDueDate> Rules) : IStatutoryParameterSet<GstReturnDueDates>
{
    public static string Key => "gst.return-due-dates";

    public static GstReturnDueDates Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<GstReturnDueDate>());
}
