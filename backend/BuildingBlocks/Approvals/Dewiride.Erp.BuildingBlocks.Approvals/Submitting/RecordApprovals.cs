using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Persistence;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Submitting;

// A request is added to the module's own context, which its unit of work saves with the record; the kind's setting is read on the
// approvals' own context, which administrators change apart from any request. Both calls look at the rows the unit of work already
// tracks first, because a query never returns a row not yet saved: a request submitted earlier in the same unit is a second one,
// and a request withdrawn earlier in it still waits in the database, where its row would refuse the new one, so submitting again
// there throws. The person who submits is the request's actor, the system when a job submits. A call that breaks these rules is a
// mistake in the module's code rather than anything a person did, so it throws.
internal sealed class RecordApprovals(ApprovalKindCatalog catalog, ApprovalsDbContext approvals, ApprovalRouting routing, IActorContext actor, TimeProvider timeProvider) : IApprovals
{
    public async Task<Result<ApprovalSubmitted>> SubmitAsync(ModuleDbContext context, string kind, Guid subjectId, Guid requestedFor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (subjectId == Guid.Empty)
        {
            throw new ArgumentException("A request approves a record, so it needs the record's id.", nameof(subjectId));
        }

        if (requestedFor == Guid.Empty || requestedFor == ActorIds.System || requestedFor == ActorIds.Anonymous)
        {
            throw new ArgumentException("A request is for a person, named by their Microsoft Entra object id.", nameof(requestedFor));
        }

        var registration = RegistrationOf(context, kind, nameof(SubmitAsync));
        var requests = context.Set<ApprovalRequest>();
        var tracked = requests.Local.Where(request => request.Kind == kind && request.SubjectId == subjectId).ToList();
        if (tracked.Exists(request => WithdrawnUnsaved(context, request)))
        {
            throw new InvalidOperationException($"The waiting request of {kind} for the record {subjectId} was withdrawn in this unit of work and is not saved yet; submit the record again in a unit of work of its own.");
        }

        if (tracked.Exists(request => request.State == ApprovalState.Submitted)
            || await requests.AnyAsync(request => request.Kind == kind && request.SubjectId == subjectId && request.State == ApprovalState.Submitted, cancellationToken).ConfigureAwait(false))
        {
            return ApprovalErrors.AlreadySubmitted(registration.Kind);
        }

        var setting = await approvals.KindSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Kind == kind, cancellationToken).ConfigureAwait(false);
        if (setting is { Required: false })
        {
            return new ApprovalSubmitted(null, ApprovalState.Approved);
        }

        var now = timeProvider.GetUtcNow();
        var workingDays = setting?.WorkingDaysToDecide ?? registration.Kind.WorkingDaysToDecide;
        var steps = await routing.PlanAsync(registration.Kind, workingDays, requestedFor, actor.ActorId, IndianStandardTime.IstDate(now), cancellationToken).ConfigureAwait(false);
        var submitted = ApprovalRequest.Submit(kind, registration.Kind.SubjectType, subjectId, requestedFor, steps, now);
        requests.Add(submitted);

        return new ApprovalSubmitted(submitted.Id, ApprovalState.Submitted);
    }

    public async Task<Result> WithdrawAsync(ModuleDbContext context, string kind, Guid subjectId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        RegistrationOf(context, kind, nameof(WithdrawAsync));
        if (context.ChangeTracker.Entries().Any(entry => entry.Entity is not (ApprovalRequest or ApprovalStep)))
        {
            throw new InvalidOperationException($"{nameof(WithdrawAsync)} comes first in a command, before it reads its record or takes a lock of its own, so that it waits for a decision being saved and the command acts on the record that decision left; {context.GetType().Name} already tracks rows of the command.");
        }

        var requests = context.Set<ApprovalRequest>();
        var now = timeProvider.GetUtcNow();
        var unsaved = requests.Local.FirstOrDefault(request => request.Kind == kind && request.SubjectId == subjectId && request.State == ApprovalState.Submitted && context.Entry(request).State == EntityState.Added);
        if (unsaved is not null)
        {
            return unsaved.Withdraw(null, now);
        }

        var waiting = await requests
            .AsNoTracking()
            .Where(request => request.Kind == kind && request.SubjectId == subjectId && request.State == ApprovalState.Submitted)
            .Select(request => request.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (waiting == default)
        {
            return Result.Success();
        }

        if (!await ApprovalLock.TryAcquireAsync(context, waiting, cancellationToken).ConfigureAwait(false))
        {
            return ApprovalErrors.Busy;
        }

        var request = await requests.Include(r => r.Steps).SingleAsync(r => r.Id == waiting, cancellationToken).ConfigureAwait(false);

        return request.State == ApprovalState.Submitted ? request.Withdraw(null, now) : Result.Success();
    }

    public async Task<ApprovalStatus?> StatusOfAsync(ModuleDbContext context, string kind, Guid subjectId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        RegistrationOf(context, kind, method: null);
        var latest = await context.Set<ApprovalRequest>()
            .AsNoTracking()
            .Where(request => request.Kind == kind && request.SubjectId == subjectId)
            .OrderByDescending(request => request.SubmittedAt)
            .ThenByDescending(request => request.Id)
            .Select(request => new
            {
                request.Id,
                request.State,
                request.CurrentStep,
                Waiting = request.Steps.Where(step => step.State == ApprovalStepState.Waiting).Select(step => new { step.Title, step.DueAt }).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return latest is null ? null : new ApprovalStatus(latest.Id, latest.State, latest.CurrentStep, latest.Waiting?.Title, latest.Waiting?.DueAt);
    }

    private static bool WithdrawnUnsaved(DbContext context, ApprovalRequest request)
    {
        var entry = context.Entry(request);

        return entry.State == EntityState.Modified
            && entry.Property(r => r.State).OriginalValue == ApprovalState.Submitted
            && request.State != ApprovalState.Submitted;
    }

    private ApprovalKindRegistration RegistrationOf(ModuleDbContext context, string kind, string? method)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        if (method is not null && ApprovalDecisionScope.IsRunning)
        {
            throw new InvalidOperationException($"{method} ran while a decision was being taken; an outcome handler moves its record through the module's own command and neither asks for nor withdraws an approval.");
        }

        var registration = catalog.Find(kind)
            ?? throw new InvalidOperationException($"{kind} is not a kind of approval; declare it with {nameof(ApprovalsServiceCollectionExtensions.AddApprovalKind)} in its module's services.");
        if (context.GetType() != registration.ContextType)
        {
            throw new InvalidOperationException($"The requests of {kind} are saved in {registration.ContextType.Name}, not in {context.GetType().Name}.");
        }

        if (context.Model.FindEntityType(typeof(ApprovalRequest)) is null || context.Model.FindEntityType(typeof(ApprovalStep)) is null)
        {
            throw new InvalidOperationException($"{context.GetType().Name} maps no approvals; declare `public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();` and `public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();`.");
        }

        if (method is not null && context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException($"{method} of {kind} was called outside a unit of work; call it in the command that changes the record, so the request commits with the record.");
        }

        return registration;
    }
}
