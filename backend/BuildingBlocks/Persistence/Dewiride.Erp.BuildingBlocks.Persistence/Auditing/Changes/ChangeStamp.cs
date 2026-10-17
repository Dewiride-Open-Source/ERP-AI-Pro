namespace Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

internal readonly record struct ChangeStamp(DateTimeOffset OccurredAt, Guid ActorId, string? ClientAddress, string? CorrelationId);
