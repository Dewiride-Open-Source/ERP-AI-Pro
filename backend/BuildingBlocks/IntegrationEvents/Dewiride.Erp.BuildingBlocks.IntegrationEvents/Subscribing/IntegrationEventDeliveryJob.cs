using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Telemetry;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Kernel.Events;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Catalog;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;

// One delivery of one event to one subscriber, in the unit of work of the subscriber's module, which inserts the processed record
// together with the subscriber's changes. A delivery that runs again finds the record and succeeds without calling the handler;
// two runs of one delivery at once both reach the insert, and the loser's conflict is retried by the runner and then finds it.
internal sealed partial class IntegrationEventDeliveryJob<TEvent, THandler>(
    IServiceProvider services,
    DbContextCatalog catalog,
    DomainEventDispatcher dispatcher,
    UnitOfWorkSignal signal,
    IntegrationEventCatalog subscriptions,
    TimeProvider timeProvider,
    ILogger<IntegrationEventDeliveryJob<TEvent, THandler>> logger) : IJob<IntegrationEventDelivery>
    where TEvent : class, IIntegrationEvent
    where THandler : class, IIntegrationEventHandler<TEvent>
{
    public async Task<Result> RunAsync(IntegrationEventDelivery payload, JobContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(context);

        var eventId = IntegrationEventId.From(payload.EventId);
        using var scope = logger.BeginScope(new IntegrationEventScope(eventId, payload.EventName, payload.Source, payload.CorrelationId));
        if (!IntegrationEventSerializer.TryDeserialize<TEvent>(payload.Event, out var integrationEvent))
        {
            LogUnreadable(typeof(TEvent).Name);
            return IntegrationEventErrors.Unreadable;
        }

        var subscriber = subscriptions.Of(typeof(THandler)).Subscriber;
        var registration = catalog.OwnedBy(typeof(THandler).Assembly)
            ?? throw new InvalidOperationException($"{typeof(THandler).Name} lives in an assembly that owns no DbContext, so it has nowhere to record the events it handled.");
        var moduleContext = (DbContext)services.GetRequiredService(registration.ContextType);
        var handler = ActivatorUtilities.CreateInstance<THandler>(services);
        try
        {
            var outcome = await EfUnitOfWork.RunAsync(moduleContext, dispatcher, signal, async token =>
            {
                var processed = moduleContext.Set<ProcessedMessage>();
                if (await processed.AnyAsync(message => message.EventId == eventId && message.Subscriber == subscriber, token).ConfigureAwait(false))
                {
                    return Result.Success(false);
                }

                var handled = await handler.HandleAsync(integrationEvent, new IntegrationEventContext(eventId, payload.OccurredAt, context), token).ConfigureAwait(false);
                if (handled.IsFailure)
                {
                    return Result.Fail<bool>(handled.Error!);
                }

                processed.Add(new ProcessedMessage(eventId, subscriber, timeProvider.GetUtcNow()));

                return Result.Success(true);
            }, cancellationToken).ConfigureAwait(false);

            if (outcome.IsFailure)
            {
                return Result.Fail(outcome.Error!);
            }

            if (!outcome.Value)
            {
                LogAlreadyHandled();
            }

            return Result.Success();
        }
        finally
        {
            if (handler is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (handler is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The delivered event can no longer be read as {EventType}, so it was not handled")]
    private partial void LogUnreadable(string eventType);

    [LoggerMessage(Level = LogLevel.Information, Message = "The event was already handled by this subscriber, so the delivery changed nothing")]
    private partial void LogAlreadyHandled();
}
