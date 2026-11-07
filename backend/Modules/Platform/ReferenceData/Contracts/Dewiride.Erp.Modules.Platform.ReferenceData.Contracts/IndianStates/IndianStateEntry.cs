namespace Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;

public sealed record IndianStateEntry(string GstCode, string Name, IndianStateKind Kind, DateOnly ValidFrom, DateOnly? ValidUntil);
