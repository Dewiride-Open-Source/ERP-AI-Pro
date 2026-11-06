using System.Collections.Concurrent;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Events;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Integration.Consumers;

// What the two subscribers of a test do, shared by both runners of the test: each records a receipt unless its outcome says
// otherwise, and every call is counted, so a test sees how often the handler itself ran, which the processed records cannot show.
public sealed class SubscriberScript
{
    private readonly ConcurrentDictionary<Type, Func<ShipmentDispatched, IntegrationEventContext, CancellationToken, Task<Result>>> _outcomes = new();

    public ConcurrentQueue<(Type Subscriber, Guid ShipmentId)> Calls { get; } = new();

    public void Answer<TSubscriber>(Func<ShipmentDispatched, IntegrationEventContext, CancellationToken, Task<Result>> outcome) => _outcomes[typeof(TSubscriber)] = outcome;

    public Task<Result> OutcomeOf(Type subscriber, ShipmentDispatched integrationEvent, IntegrationEventContext context, CancellationToken cancellationToken)
    {
        Calls.Enqueue((subscriber, integrationEvent.ShipmentId));

        return _outcomes.TryGetValue(subscriber, out var outcome) ? outcome(integrationEvent, context, cancellationToken) : Task.FromResult(Result.Success());
    }

    public int CallsOf<TSubscriber>(Guid shipmentId) => Calls.Count(call => call.Subscriber == typeof(TSubscriber) && call.ShipmentId == shipmentId);
}
