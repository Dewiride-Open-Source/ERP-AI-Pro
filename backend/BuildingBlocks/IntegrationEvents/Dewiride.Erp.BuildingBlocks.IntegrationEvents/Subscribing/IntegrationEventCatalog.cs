namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;

// The subscribers this release defines, by the events they handle. A subscriber removed from a release leaves its pending
// deliveries to be cancelled on the jobs page, because a runner claims only the runs of jobs its release defines.
internal sealed class IntegrationEventCatalog
{
    private readonly Dictionary<string, IReadOnlyList<IntegrationEventSubscription>> _byEvent;

    private readonly Dictionary<Type, IntegrationEventSubscription> _byHandler = [];

    public IntegrationEventCatalog(IEnumerable<IntegrationEventSubscription> subscriptions)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);

        var all = subscriptions.ToList();
        foreach (var subscription in all)
        {
            if (!_byHandler.TryAdd(subscription.HandlerType, subscription))
            {
                throw new InvalidOperationException($"{subscription.HandlerType.Name} is registered as a subscriber more than once.");
            }
        }

        _byEvent = all.GroupBy(subscription => subscription.EventName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, IReadOnlyList<IntegrationEventSubscription> (group) => [.. group.OrderBy(subscription => subscription.Subscriber, StringComparer.Ordinal)], StringComparer.Ordinal);
        Subscriptions = [.. all.OrderBy(subscription => subscription.Subscriber, StringComparer.Ordinal)];
    }

    public IReadOnlyList<IntegrationEventSubscription> Subscriptions { get; }

    public IReadOnlyList<IntegrationEventSubscription> SubscribersOf(string eventName) => _byEvent.GetValueOrDefault(eventName) ?? [];

    public IntegrationEventSubscription Of(Type handlerType) =>
        _byHandler.GetValueOrDefault(handlerType) ?? throw new InvalidOperationException($"{handlerType.Name} is not a registered subscriber; register it with AddIntegrationEventHandler.");
}
