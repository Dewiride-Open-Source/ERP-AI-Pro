using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Publishing;

// The delivery a scope runs, so an event its subscriber publishes counts the deliveries that led to it. Subscribers whose work leads
// back to an event they handle would otherwise publish forever, each hop passed on at once; past MaxDepth the publication is
// refused, so the delivery fails and, after its retries, is dead-lettered, as the unit of work stops a cycle of domain events.
internal sealed class IntegrationEventChain
{
    public const int MaxDepth = 10;

    private IntegrationEventDelivery? _delivery;

    public void Deliver(IntegrationEventDelivery delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        _delivery = delivery;
    }

    public int DepthOf(string eventName)
    {
        if (_delivery is null)
        {
            return 0;
        }

        var depth = _delivery.Depth + 1;
        if (depth > MaxDepth)
        {
            throw new InvalidOperationException($"{eventName} would follow a chain of {depth} deliveries, each publishing the next event, and a chain is at most {MaxDepth}; the subscribers of {_delivery.EventName} lead back to an event they handle.");
        }

        return depth;
    }
}
