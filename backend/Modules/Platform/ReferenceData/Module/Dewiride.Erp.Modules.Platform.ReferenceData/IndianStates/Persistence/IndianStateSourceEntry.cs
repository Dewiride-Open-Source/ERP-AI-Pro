using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Domain;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Persistence;

internal sealed record IndianStateSourceEntry(string GstCode, string Name, TerritoryKind Kind, DateOnly ValidFrom, DateOnly? ValidUntil, string Source);
