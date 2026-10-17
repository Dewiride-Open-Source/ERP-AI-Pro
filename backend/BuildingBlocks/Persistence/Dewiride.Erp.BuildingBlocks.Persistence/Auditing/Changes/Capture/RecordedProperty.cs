using Microsoft.EntityFrameworkCore.Metadata;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes.Capture;

internal sealed record RecordedProperty(IProperty Property, string Path, ChangeValueKind Kind, string? References);
