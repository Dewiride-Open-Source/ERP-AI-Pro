using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;

internal static class IntegrationEventErrors
{
    public static readonly Error Unreadable = Error.Failure("integration-event.unreadable", "The event this delivery carries can no longer be read, so it cannot be handled.");

    public static readonly Error TooOld = Error.Failure("integration-event.too-old", "The event this delivery carries is older than the records of handled events are kept, so handling it could apply it twice.");
}
