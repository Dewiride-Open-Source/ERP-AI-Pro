namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Documents;

public sealed record GstDocumentSerialNumber(string Rule, string Document, int MaxLength, string SpecialCharacters, string Words);
