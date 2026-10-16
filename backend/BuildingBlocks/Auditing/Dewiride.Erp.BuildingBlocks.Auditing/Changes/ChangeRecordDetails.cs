using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

namespace Dewiride.Erp.BuildingBlocks.Auditing.Changes;

public sealed record ChangeRecordDetails(
    Guid Id,
    DateTimeOffset OccurredAt,
    ChangeOperation Operation,
    string EntityType,
    Guid EntityId,
    string SubjectType,
    Guid SubjectId,
    string? SubjectName,
    ChangeActorKind ActorKind,
    Guid ActorId,
    string? ActorName,
    string? ClientAddress,
    string? CorrelationId,
    IReadOnlyList<ChangedValueDetails> Changes);
