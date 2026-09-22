using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.Application.Pipeline;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;

public static class EfUnitOfWork
{
    public const string ConcurrencyConflictCode = "concurrency.conflict";

    public const int MaxDispatchRounds = 10;

    public static async Task<Result<TResult>> RunAsync<TResult>(DbContext context, DomainEventDispatcher dispatcher, UnitOfWorkSignal signal, PipelineContinuation<TResult> handler, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(handler);

        if (context.Database.CurrentTransaction is not null)
        {
            return await CommitAsync(context, dispatcher, handler, cancellationToken).ConfigureAwait(false);
        }

        return await context.Database.CreateExecutionStrategy().ExecuteAsync(
            async token =>
            {
                context.ChangeTracker.Clear();
                await using var transaction = await context.Database.BeginTransactionAsync(token).ConfigureAwait(false);
                var result = await CommitAsync(context, dispatcher, handler, token).ConfigureAwait(false);
                if (result.IsFailure)
                {
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                    return result;
                }

                await transaction.CommitAsync(token).ConfigureAwait(false);
                signal.MarkCommitted();

                return result;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<TResult>> CommitAsync<TResult>(DbContext context, DomainEventDispatcher dispatcher, PipelineContinuation<TResult> handler, CancellationToken cancellationToken)
    {
        var result = await handler(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return result;
        }

        try
        {
            for (var round = 0; ; round++)
            {
                var events = TakeDomainEvents(context);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                if (events.Count == 0)
                {
                    return result;
                }

                if (round == MaxDispatchRounds)
                {
                    throw new InvalidOperationException($"Domain event handlers kept raising further events after {MaxDispatchRounds} rounds; the handlers of {string.Join(", ", events.Select(domainEvent => domainEvent.GetType().Name).Distinct())} form a cycle.");
                }

                foreach (var domainEvent in events)
                {
                    await dispatcher.DispatchAsync(domainEvent, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return Error.Conflict(ConcurrencyConflictCode, $"{string.Join(", ", exception.Entries.Select(entry => entry.Metadata.ClrType.Name).Distinct())} changed since it was read; reload and try again.");
        }
    }

    private static List<IDomainEvent> TakeDomainEvents(DbContext context)
    {
        var sources = context.ChangeTracker.Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();
        var events = sources.SelectMany(source => source.DomainEvents).ToList();
        foreach (var source in sources)
        {
            source.ClearDomainEvents();
        }

        return events;
    }
}
