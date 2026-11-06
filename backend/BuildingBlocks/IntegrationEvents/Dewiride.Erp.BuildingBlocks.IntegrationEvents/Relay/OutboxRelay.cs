using System.Collections.Concurrent;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.BuildingBlocks.Jobs.Running;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Relay;

// Passes committed events on as one delivery run per subscriber, at the job runner's poll. Reading every outbox at every poll
// would write a command line of each module's context every PollInterval, so the relay reads only when an event committed in
// this process since its last pass (Announce, after the commit), at its first poll after the start, at the poll after a pass in
// which an event or an outbox failed for the first time, and once every FullPassInterval, which also finds an event whose process
// stopped between its commit and the announcement and retries an event or an outbox that keeps failing. Each event and each
// outbox is passed on in its own try/catch, so one that keeps failing never holds back the others. An enqueue under the event's
// key answers the run an earlier pass made, so a pass that stopped between the enqueue and the mark makes no second delivery.
internal sealed partial class OutboxRelay(
    IServiceScopeFactory scopes,
    IntegrationEventTables tables,
    IntegrationEventCatalog subscriptions,
    IJobRunnerSignal runner,
    TimeProvider timeProvider,
    ILogger<OutboxRelay> logger) : IJobRunSource
{
    public const int BatchSize = 100;

    public const string KeyPrefix = "event:";

    public static readonly TimeSpan FullPassInterval = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<IntegrationEventId, int> _failingEvents = new();

    private readonly ConcurrentDictionary<Type, int> _failingOutboxes = new();

    private int _due = 1;

    private DateTimeOffset _lastPass = DateTimeOffset.MinValue;

    public static string KeyOf(IntegrationEventId id) => $"{KeyPrefix}{id.Value:D}";

    public void Announce()
    {
        Volatile.Write(ref _due, 1);
        runner.Wake();
    }

    public async Task EnqueueDueAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (Interlocked.Exchange(ref _due, 0) == 0 && now - _lastPass < FullPassInterval)
        {
            return;
        }

        _lastPass = now;
        var newFailures = 0;
        foreach (var contextType in tables.Outboxes)
        {
            newFailures += await RelayAsync(contextType, cancellationToken).ConfigureAwait(false);
        }

        if (newFailures > 0)
        {
            Volatile.Write(ref _due, 1);
        }
    }

    internal async Task RelayOneAsync(Type contextType, IntegrationEventId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contextType);

        await using var scope = scopes.CreateAsyncScope();
        var context = (ModuleDbContext)scope.ServiceProvider.GetRequiredService(contextType);
        var message = await context.Set<OutboxMessage>().AsNoTracking().SingleOrDefaultAsync(m => m.Id == id && m.RelayedAt == null, cancellationToken).ConfigureAwait(false);
        if (message is not null)
        {
            await PassOnAsync(context, scope.ServiceProvider.GetRequiredService<IJobQueue>(), message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<int> RelayAsync(Type contextType, CancellationToken cancellationToken)
    {
        var newFailures = 0;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var context = (ModuleDbContext)scope.ServiceProvider.GetRequiredService(contextType);
            var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
            var failed = new List<IntegrationEventId>();
            List<OutboxMessage> page;
            do
            {
                page = await context.Set<OutboxMessage>()
                    .AsNoTracking()
                    .Where(m => m.RelayedAt == null && !failed.Contains(m.Id))
                    .OrderBy(m => m.OccurredAt)
                    .ThenBy(m => m.Id)
                    .Take(BatchSize)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                foreach (var message in page)
                {
                    var failures = await TryPassOnAsync(context, queue, message, cancellationToken).ConfigureAwait(false);
                    if (failures > 0)
                    {
                        failed.Add(message.Id);
                        newFailures += failures == 1 ? 1 : 0;
                    }
                }
            }
            while (page.Count == BatchSize);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (_failingOutboxes.AddOrUpdate(contextType, 1, static (_, count) => count + 1) == 1)
            {
                LogOutboxFailed(exception, contextType.Name);
                newFailures++;
            }

            return newFailures;
        }

        if (_failingOutboxes.TryRemove(contextType, out var earlier))
        {
            LogOutboxReadAfterFailures(contextType.Name, earlier);
        }

        return newFailures;
    }

    private async Task<int> TryPassOnAsync(ModuleDbContext context, IJobQueue queue, OutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await PassOnAsync(context, queue, message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failures = _failingEvents.AddOrUpdate(message.Id, 1, static (_, count) => count + 1);
            if (failures == 1)
            {
                LogPassOnFailed(exception, message.EventName, message.Id.Value, context.Schema);
            }

            return failures;
        }

        if (_failingEvents.TryRemove(message.Id, out var earlier))
        {
            LogPassedOnAfterFailures(message.EventName, message.Id.Value, context.Schema, earlier);
        }

        return 0;
    }

    private async Task PassOnAsync(ModuleDbContext context, IJobQueue queue, OutboxMessage message, CancellationToken cancellationToken)
    {
        var subscribers = subscriptions.SubscribersOf(message.EventName);
        if (subscribers.Count > 0)
        {
            using var document = JsonDocument.Parse(message.Payload);
            var delivery = new IntegrationEventDelivery(message.Id.Value, message.EventName, context.Schema, message.OccurredAt, message.CorrelationId, message.Depth, document.RootElement.Clone());
            foreach (var subscription in subscribers)
            {
                await subscription.Enqueue(queue, delivery, KeyOf(message.Id), cancellationToken).ConfigureAwait(false);
            }
        }

        var relayedAt = timeProvider.GetUtcNow();
        await context.Set<OutboxMessage>()
            .Where(m => m.Id == message.Id && m.RelayedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.RelayedAt, relayedAt), cancellationToken)
            .ConfigureAwait(false);
        LogPassedOn(message.EventName, message.Id.Value, context.Schema, subscribers.Count);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Passed on {EventName} {EventId} of {Source} to {Subscribers} subscribers")]
    private partial void LogPassedOn(string eventName, Guid eventId, string source, int subscribers);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not pass on {EventName} {EventId} of {Source}; the relay tries again at a later pass, and the events behind it go on")]
    private partial void LogPassOnFailed(Exception exception, string eventName, Guid eventId, string source);

    [LoggerMessage(Level = LogLevel.Information, Message = "Passed on {EventName} {EventId} of {Source} after {Failures} failed passes")]
    private partial void LogPassedOnAfterFailures(string eventName, Guid eventId, string source, int failures);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not read the outbox of {Context}; the relay tries again at a later pass, and the other outboxes go on")]
    private partial void LogOutboxFailed(Exception exception, string context);

    [LoggerMessage(Level = LogLevel.Information, Message = "Read the outbox of {Context} again after {Failures} failed passes")]
    private partial void LogOutboxReadAfterFailures(string context, int failures);
}
