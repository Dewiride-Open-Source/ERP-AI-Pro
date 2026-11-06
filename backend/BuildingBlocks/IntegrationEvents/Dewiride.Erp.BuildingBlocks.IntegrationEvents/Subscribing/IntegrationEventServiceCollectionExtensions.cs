using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Kernel.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;

// Each subscriber is a job of its own, named by its definition under the consuming module's prefix, so its deliveries are retried,
// dead-lettered, re-run and cancelled on the jobs page apart from every other subscriber's, and its retry policy and attempt
// timeout are the definition's. The handler is created for each attempt, never registered, as a job is.
public static class IntegrationEventServiceCollectionExtensions
{
    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(this IServiceCollection services, JobDefinition definition)
        where TEvent : class, IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(definition);

        var eventName = IntegrationEventNames.Of<TEvent>();
        services.AddJob<IntegrationEventDeliveryJob<TEvent, THandler>, IntegrationEventDelivery>(definition);
        services.AddSingleton(new IntegrationEventSubscription(
            eventName,
            definition.Name,
            typeof(THandler),
            static (queue, delivery, key, cancellationToken) => queue.EnqueueAsync<IntegrationEventDeliveryJob<TEvent, THandler>, IntegrationEventDelivery>(delivery, key, cancellationToken)));

        return services;
    }
}
