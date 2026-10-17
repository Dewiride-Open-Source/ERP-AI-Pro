using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

namespace Dewiride.Erp.BuildingBlocks.Auditing.Changes;

internal sealed record StoredChange(string Property, ChangeValueKind Kind, string? Before, string? After, string? References);
