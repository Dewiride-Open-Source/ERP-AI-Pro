using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;

internal sealed record IntegrationEventSubscription(
    string EventName,
    string Subscriber,
    Type HandlerType,
    Func<IJobQueue, IntegrationEventDelivery, string, CancellationToken, Task<JobRunId>> Enqueue);
