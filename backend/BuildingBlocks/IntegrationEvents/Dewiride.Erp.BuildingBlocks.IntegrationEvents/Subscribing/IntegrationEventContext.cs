using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;

public sealed record IntegrationEventContext(IntegrationEventId EventId, DateTimeOffset OccurredAt, JobContext Job);
