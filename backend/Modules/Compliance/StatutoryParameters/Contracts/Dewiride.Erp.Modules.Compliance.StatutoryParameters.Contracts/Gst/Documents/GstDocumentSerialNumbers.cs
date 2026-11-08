namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Documents;

public sealed record GstDocumentSerialNumbers(StatutoryParameterVersion Version, IReadOnlyList<GstDocumentSerialNumber> Rules) : IStatutoryParameterSet<GstDocumentSerialNumbers>
{
    public static string Key => "gst.document-serial-numbers";

    public static GstDocumentSerialNumbers Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<GstDocumentSerialNumber>());
}
