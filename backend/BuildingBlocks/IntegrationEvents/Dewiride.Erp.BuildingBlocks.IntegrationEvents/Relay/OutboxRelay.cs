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
// this process since its last pass (Announce, after the commit), at its first poll after the start, after a pass in which an
// event could not be passed on, and once every FullPassInterval, which also finds an event whose process stopped between its
// commit and the announcement. Each event is passed on in its own try/catch, so one that keeps failing never holds back the
// events behind it. An enqueue under the event's key answers the run an earlier pass made, so a pass that stopped between the
// enqueue and the mark makes no second delivery.
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

    private readonly ConcurrentDictionary<IntegrationEventId, int> _failing = new();

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
        var complete = false;
        try
        {
            var passedOnEveryEvent = true;
            foreach (var contextType in tables.Outboxes)
            {
                passedOnEveryEvent &= await RelayAsync(contextType, cancellationToken).ConfigureAwait(false);
            }

            complete = passedOnEveryEvent;
        }
        finally
        {
            if (!complete)
            {
                Volatile.Write(ref _due, 1);
            }
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

    private async Task<bool> RelayAsync(Type contextType, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = (ModuleDbContext)scope.ServiceProvider.GetRequiredService(contextType);
        var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
        var failed = new List<IntegrationEventId>();
        while (true)
        {
            var page = await context.Set<OutboxMessage>()
                .AsNoTracking()
                .Where(m => m.RelayedAt == null && !failed.Contains(m.Id))
                .OrderBy(m => m.OccurredAt)
                .ThenBy(m => m.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var message in page)
            {
                if (!await TryPassOnAsync(context, queue, message, cancellationToken).ConfigureAwait(false))
                {
                    failed.Add(message.Id);
                }
            }

            if (page.Count < BatchSize)
            {
                return failed.Count == 0;
            }
        }
    }

    private async Task<bool> TryPassOnAsync(ModuleDbContext context, IJobQueue queue, OutboxMessage message, CancellationToken cancellationToken)
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
            if (_failing.AddOrUpdate(message.Id, 1, static (_, failures) => failures + 1) == 1)
            {
                LogPassOnFailed(exception, message.EventName, message.Id.Value, context.Schema);
            }

            return false;
        }

        if (_failing.TryRemove(message.Id, out var failures))
        {
            LogPassedOnAfterFailures(message.EventName, message.Id.Value, context.Schema, failures);
        }

        return true;
    }

    private async Task PassOnAsync(ModuleDbContext context, IJobQueue queue, OutboxMessage message, CancellationToken cancellationToken)
    {
        var subscribers = subscriptions.SubscribersOf(message.EventName);
        if (subscribers.Count > 0)
        {
            using var document = JsonDocument.Parse(message.Payload);
            var delivery = new IntegrationEventDelivery(message.Id.Value, message.EventName, context.Schema, message.OccurredAt, message.CorrelationId, document.RootElement.Clone());
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not pass on {EventName} {EventId} of {Source}; the relay tries again at its next pass, and the events behind it go on")]
    private partial void LogPassOnFailed(Exception exception, string eventName, Guid eventId, string source);

    [LoggerMessage(Level = LogLevel.Information, Message = "Passed on {EventName} {EventId} of {Source} after {Failures} failed passes")]
    private partial void LogPassedOnAfterFailures(string eventName, Guid eventId, string source, int failures);
}
