using System.Collections.Concurrent;
using System.Reflection;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.Application.Events;

public sealed class DomainEventDispatcher(IServiceProvider services)
{
    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, IDomainEvent, CancellationToken, Task>> Dispatchers = new();

    private static readonly MethodInfo DispatchTypedMethod = typeof(DomainEventDispatcher).GetMethod(nameof(DispatchTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return Dispatchers.GetOrAdd(domainEvent.GetType(), CreateDispatcher)(services, domainEvent, cancellationToken);
    }

    private static Func<IServiceProvider, IDomainEvent, CancellationToken, Task> CreateDispatcher(Type eventType) =>
        DispatchTypedMethod.MakeGenericMethod(eventType).CreateDelegate<Func<IServiceProvider, IDomainEvent, CancellationToken, Task>>();

    private static async Task DispatchTypedAsync<TEvent>(IServiceProvider services, IDomainEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
        {
            await handler.HandleAsync((TEvent)domainEvent, cancellationToken).ConfigureAwait(false);
        }
    }
}
