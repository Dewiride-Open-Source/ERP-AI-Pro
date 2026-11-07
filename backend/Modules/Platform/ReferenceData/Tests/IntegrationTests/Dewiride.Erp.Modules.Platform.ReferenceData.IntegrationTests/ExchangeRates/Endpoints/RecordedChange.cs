using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IntegrationTests.ExchangeRates.Endpoints;

internal sealed record RecordedChange(
    ChangeOperation Operation,
    string EntityType,
    Guid EntityId,
    string SubjectType,
    string? SubjectName,
    Guid ActorId,
    IReadOnlyList<RecordedValue> Changes);
