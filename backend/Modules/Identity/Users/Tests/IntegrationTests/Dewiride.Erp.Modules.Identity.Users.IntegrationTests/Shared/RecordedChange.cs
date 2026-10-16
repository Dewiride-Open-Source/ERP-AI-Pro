using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;

internal sealed record RecordedChange(
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
    IReadOnlyList<RecordedValue> Changes)
{
    public IEnumerable<RecordedValue> ValuesByProperty => Changes.OrderBy(value => value.Property, StringComparer.Ordinal);
}
