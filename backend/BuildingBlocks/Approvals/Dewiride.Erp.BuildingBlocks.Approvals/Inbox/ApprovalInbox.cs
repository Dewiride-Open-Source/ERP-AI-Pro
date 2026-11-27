using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;
using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Persistence;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Queries;
using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Inbox;

// The lists read the requests of every kind on the approvals' own context, each joined to its waiting step, so a list sorts by
// due time in SQL and pages there. A request is late from its waiting step's due time on, measured when it is read, because lateness
// is shown rather than stored. A request may be decided or withdrawn here only while it waits and its kind is declared by this
// release.
internal sealed class ApprovalInbox(
    ApprovalsDbContext context,
    ApprovalKindCatalog catalog,
    IEnumerable<IRecordNames> recordNames,
    IEnumerable<IPersonNames> personNames,
    TimeProvider timeProvider) : IApprovalInbox
{
    public const int EarlierRequestsShown = 10;

    private static readonly SortableFields<ApprovalRequestRow> Sortable = new SortableFields<ApprovalRequestRow>()
        .Add("submittedAt", row => row.SubmittedAt)
        .Add("dueAt", row => row.DueAt)
        .Add("closedAt", row => row.ClosedAt);

    private static readonly FilterableFields<ApprovalRequestRow> Filterable = new FilterableFields<ApprovalRequestRow>()
        .Add("kind", row => row.Kind, FilterOperators.Equality)
        .Add("state", row => row.State, FilterOperators.Equality)
        .Add("subjectId", row => row.SubjectId, FilterOperators.Equality);

    private static readonly SortRequest LateFirst = new([new SortTerm("dueAt", SortDirection.Ascending)]);

    private static readonly SortRequest NewestFirst = new([new SortTerm("submittedAt", SortDirection.Descending)]);

    public async Task<Result<PagedResult<ApprovalRequestSummary>>> ListAsync(ApprovalBox box, ListRequest list, ApprovalCaller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(caller);

        if (box == ApprovalBox.All && !caller.SeesAll)
        {
            return ApprovalErrors.AllRequestsForbidden;
        }

        var sort = (list.Sort.Terms.Count > 0 ? list.Sort : box == ApprovalBox.Waiting ? LateFirst : NewestFirst).Resolve(Sortable);
        if (sort.IsFailure)
        {
            return sort.Error!;
        }

        var filter = list.Filter.Resolve(Filterable);
        if (filter.IsFailure)
        {
            return filter.Error!;
        }

        var page = await RowsOf(ApprovalVisibility.In(context.Requests.AsNoTracking(), box, caller))
            .ApplyFilter(filter.Value)
            .ApplySort(sort.Value, row => row.Id)
            .ToPagedResultAsync(list.Page, cancellationToken)
            .ConfigureAwait(false);
        var names = await ApprovalNames.ResolveAsync(
            [.. page.Items.Select(row => (row.SubjectType, row.SubjectId))],
            [.. page.Items.SelectMany(PeopleOf)],
            recordNames,
            personNames,
            cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();

        return page.Map(row => SummaryOf(row, names, now));
    }

    public async Task<Result<ApprovalRequestDetails>> GetAsync(ApprovalRequestId id, ApprovalCaller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var request = await context.Requests.AsNoTracking().Include(r => r.Steps).SingleOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);
        if (request is null || !ApprovalVisibility.IsVisibleTo(request, caller))
        {
            return ApprovalErrors.RequestNotFound;
        }

        var earlier = await context.Requests
            .AsNoTracking()
            .Where(r => r.Kind == request.Kind && r.SubjectId == request.SubjectId && r.SubmittedAt < request.SubmittedAt)
            .OrderByDescending(r => r.SubmittedAt)
            .ThenByDescending(r => r.Id)
            .Take(EarlierRequestsShown)
            .Select(r => new EarlierApprovalRequest(r.Id, r.State, r.SubmittedAt, r.ClosedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var steps = request.Steps.OrderBy(step => step.Order).ToList();
        var row = RowOf(request);
        var names = await ApprovalNames.ResolveAsync(
            [(request.SubjectType, request.SubjectId)],
            [.. PeopleOf(row), .. steps.SelectMany(step => new[] { step.ApproverId, step.DecidedBy }).OfType<Guid>()],
            recordNames,
            personNames,
            cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var open = request.State == ApprovalState.Submitted && catalog.Find(request.Kind) is not null;
        var eligibility = ApprovalRouting.EligibilityOf(request, caller);

        return new ApprovalRequestDetails(
            SummaryOf(row, names, now),
            RecordVersion.Encode(request.RowVersion),
            [.. steps.Select(step => StepOf(step, names, now))],
            earlier,
            open && eligibility is ApprovalEligibility.Approver or ApprovalEligibility.InPlaceOfApprover,
            open && eligibility == ApprovalEligibility.InPlaceOfApprover,
            open && ApprovalRouting.IsOwn(request, caller.ObjectId));
    }

    public async Task<ApprovalCounts> CountAsync(ApprovalCaller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var now = timeProvider.GetUtcNow();
        var waiting = context.Requests.AsNoTracking().Where(ApprovalVisibility.WaitingFor(caller));
        var waitingForMe = await waiting.CountAsync(cancellationToken).ConfigureAwait(false);
        var lateForMe = await waiting
            .CountAsync(request => request.Steps.Any(step => step.State == ApprovalStepState.Waiting && step.DueAt <= now), cancellationToken)
            .ConfigureAwait(false);
        var mineWaiting = await context.Requests
            .AsNoTracking()
            .Where(ApprovalVisibility.AskedOrSubmittedBy(caller.ObjectId))
            .CountAsync(request => request.State == ApprovalState.Submitted, cancellationToken)
            .ConfigureAwait(false);

        return new ApprovalCounts(waitingForMe, lateForMe, mineWaiting);
    }

    private static IEnumerable<Guid> PeopleOf(ApprovalRequestRow row) =>
        row.WaitingForId is { } approver ? [row.RequestedFor, row.SubmittedBy, approver] : [row.RequestedFor, row.SubmittedBy];

    private static ApprovalRequestRow RowOf(ApprovalRequest request)
    {
        var waiting = request.WaitingStep;

        return new ApprovalRequestRow
        {
            Id = request.Id,
            Kind = request.Kind,
            SubjectType = request.SubjectType,
            SubjectId = request.SubjectId,
            RequestedFor = request.RequestedFor,
            SubmittedBy = request.CreatedBy,
            SubmittedAt = request.SubmittedAt,
            State = request.State,
            ClosedAt = request.ClosedAt,
            WaitingStepTitle = waiting?.Title,
            WaitingForKind = waiting?.ApproverKind,
            WaitingForId = waiting?.ApproverId,
            WaitingForRole = waiting?.ApproverRole,
            DueAt = waiting?.DueAt,
        };
    }

    private static ApprovalStepDetails StepOf(ApprovalStep step, ApprovalNames names, DateTimeOffset now) =>
        new(
            step.Order,
            step.Title,
            step.Rule,
            step.State,
            step.ApproverKind,
            step.ApproverId,
            names.PersonName(step.ApproverId),
            step.ApproverRole,
            step.WorkingDays,
            step.StartedAt,
            step.DueAt,
            step.IsLateAt(now),
            step.DecidedBy,
            names.PersonName(step.DecidedBy),
            step.DecidedAt,
            step.InPlaceOfApprover,
            step.Comment);

    private IQueryable<ApprovalRequestRow> RowsOf(IQueryable<ApprovalRequest> requests) =>
        requests.LeftJoin(
            context.Steps.AsNoTracking().Where(step => step.State == ApprovalStepState.Waiting),
            request => request.Id,
            step => step.RequestId,
            (request, step) => new ApprovalRequestRow
            {
                Id = request.Id,
                Kind = request.Kind,
                SubjectType = request.SubjectType,
                SubjectId = request.SubjectId,
                RequestedFor = request.RequestedFor,
                SubmittedBy = request.CreatedBy,
                SubmittedAt = request.SubmittedAt,
                State = request.State,
                ClosedAt = request.ClosedAt,
                WaitingStepTitle = step == null ? null : step.Title,
                WaitingForKind = step == null ? null : (ApproverKind?)step.ApproverKind,
                WaitingForId = step == null ? null : step.ApproverId,
                WaitingForRole = step == null ? null : step.ApproverRole,
                DueAt = step == null ? null : step.DueAt,
            });

    private ApprovalRequestSummary SummaryOf(ApprovalRequestRow row, ApprovalNames names, DateTimeOffset now) =>
        new(
            row.Id,
            row.Kind,
            catalog.TitleOf(row.Kind),
            row.SubjectType,
            row.SubjectId,
            names.RecordName(row.SubjectType, row.SubjectId),
            row.RequestedFor,
            names.PersonName(row.RequestedFor),
            row.SubmittedBy,
            names.PersonName(row.SubmittedBy),
            row.SubmittedAt,
            row.State,
            row.WaitingStepTitle,
            row.WaitingForKind,
            row.WaitingForId,
            names.PersonName(row.WaitingForId),
            row.WaitingForRole,
            row.DueAt,
            row.State == ApprovalState.Submitted && row.DueAt is { } due && now >= due,
            row.ClosedAt);
}
