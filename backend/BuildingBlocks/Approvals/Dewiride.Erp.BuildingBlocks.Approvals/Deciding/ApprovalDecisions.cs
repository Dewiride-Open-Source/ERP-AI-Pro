using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;
using Dewiride.Erp.BuildingBlocks.Approvals.Outcomes;
using Dewiride.Erp.BuildingBlocks.Approvals.Persistence;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Deciding;

// The request is read first on the approvals' own context, to learn its kind and whether the caller may see it, and again under
// its lock in the unit of work of the kind's context, where its state and version decide: of two people deciding at once one
// decides and the other finds the request closed. The outcome handler is created for each decision and runs in that unit of work,
// so the change records of the request, its steps and the record all name the caller under one correlation id.
internal sealed partial class ApprovalDecisions(
    IServiceProvider services,
    ApprovalKindCatalog catalog,
    ApprovalsDbContext approvals,
    IApprovalInbox inbox,
    DomainEventDispatcher dispatcher,
    UnitOfWorkSignal signal,
    TimeProvider timeProvider,
    ILogger<ApprovalDecisions> logger) : IApprovalDecisions
{
    public Task<Result<ApprovalRequestDetails>> DecideAsync(ApprovalRequestId id, ApprovalDecision decision, string? comment, byte[] version, ApprovalCaller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision), decision, "A request is approved, sent back or rejected.");
        }

        return RunAsync(
            id,
            version,
            caller,
            (request, now) =>
            {
                var eligibility = ApprovalRouting.EligibilityOf(request, caller);
                if (eligibility == ApprovalEligibility.OwnRequest)
                {
                    return ApprovalErrors.OwnRequest;
                }

                if (eligibility == ApprovalEligibility.None)
                {
                    return ApprovalErrors.NotYours;
                }

                var inPlaceOfApprover = eligibility == ApprovalEligibility.InPlaceOfApprover;

                return decision switch
                {
                    ApprovalDecision.Approve => request.Approve(caller.ObjectId, inPlaceOfApprover, comment, now),
                    ApprovalDecision.Return => request.Return(caller.ObjectId, inPlaceOfApprover, comment, now),
                    _ => request.Reject(caller.ObjectId, inPlaceOfApprover, comment, now),
                };
            },
            cancellationToken);
    }

    public Task<Result<ApprovalRequestDetails>> WithdrawAsync(ApprovalRequestId id, string? comment, byte[] version, ApprovalCaller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return RunAsync(
            id,
            version,
            caller,
            (request, now) => ApprovalRouting.IsOwn(request, caller.ObjectId) ? request.Withdraw(comment, now) : ApprovalErrors.NotYoursToWithdraw,
            cancellationToken);
    }

    private async Task<Result<ApprovalRequestDetails>> RunAsync(ApprovalRequestId id, byte[] version, ApprovalCaller caller, Func<ApprovalRequest, DateTimeOffset, Result> change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);

        var found = await approvals.Requests.AsNoTracking().Include(r => r.Steps).SingleOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);
        if (found is null || !ApprovalVisibility.IsVisibleTo(found, caller))
        {
            return ApprovalErrors.RequestNotFound;
        }

        if (catalog.Find(found.Kind) is not { } registration)
        {
            return ApprovalErrors.KindRetired(found.Kind);
        }

        var context = (DbContext)services.GetRequiredService(registration.ContextType);
        var decided = await ApprovalDecisionScope.RunAsync(() => EfUnitOfWork.RunAsync<(int Step, ApprovalState State)>(
            context,
            dispatcher,
            signal,
            async token =>
            {
                if (!await ApprovalLock.TryAcquireAsync(context, id, token).ConfigureAwait(false))
                {
                    return ApprovalErrors.Busy;
                }

                var request = await context.Set<ApprovalRequest>().Include(r => r.Steps).SingleOrDefaultAsync(r => r.Id == id, token).ConfigureAwait(false);
                if (request is null)
                {
                    return ApprovalErrors.RequestNotFound;
                }

                if (request.State != ApprovalState.Submitted || request.WaitingStep is not { } step)
                {
                    return ApprovalErrors.NotWaiting;
                }

                if (!request.RowVersion.AsSpan().SequenceEqual(version))
                {
                    return ApprovalErrors.Stale;
                }

                var changed = change(request, timeProvider.GetUtcNow());
                if (changed.IsFailure)
                {
                    return changed.Error!;
                }

                if (request.State != ApprovalState.Submitted)
                {
                    var handled = await HandleAsync(registration, new ApprovalOutcome(request.Kind, request.SubjectId, request.Id, request.State, caller.ObjectId, step.Comment), token).ConfigureAwait(false);
                    if (handled.IsFailure)
                    {
                        return handled.Error!;
                    }
                }

                return (step.Order, request.State);
            },
            cancellationToken)).ConfigureAwait(false);
        if (decided.IsFailure)
        {
            return decided.Error!;
        }

        LogDecided(id.Value, found.Kind, decided.Value.State, decided.Value.Step);

        return await inbox.GetAsync(id, caller, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> HandleAsync(ApprovalKindRegistration registration, ApprovalOutcome outcome, CancellationToken cancellationToken)
    {
        var handler = (IApprovalOutcomeHandler)ActivatorUtilities.CreateInstance(services, registration.HandlerType);
        try
        {
            var handled = await handler.HandleAsync(outcome, cancellationToken).ConfigureAwait(false);
            if (handled.Error is { } error && outcome.State != ApprovalState.Approved && error.Kind is not (ErrorKind.Conflict or ErrorKind.Unavailable))
            {
                throw new InvalidOperationException($"{registration.HandlerType.Name} refused a request of {registration.Kind.Key} that was {outcome.State} with {error.Code}; an outcome handler refuses an approval only, because a record must always be sent back, rejected or withdrawn.");
            }

            return handled;
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Approval request {RequestId} of {Kind} is {State} after step {Step}")]
    private partial void LogDecided(Guid requestId, string kind, ApprovalState state, int step);
}
