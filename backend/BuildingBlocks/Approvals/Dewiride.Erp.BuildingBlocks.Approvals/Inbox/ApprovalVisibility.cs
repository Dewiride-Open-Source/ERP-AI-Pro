using System.Linq.Expressions;
using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Inbox;

// A request is seen by the person it is for, the person who submitted it, anyone who decided one of its steps, anyone its waiting
// step waits for, and a person who may see every request. The lists apply the same rules in SQL that a loaded request is read with,
// and the waiting list leaves out the requests the caller asked for or submitted, which they may never decide. A closed request
// waits for nobody, so the earlier requests a request's page lists, which are all closed, are those the caller asked for,
// submitted or decided, or every one for a person who may see every request.
internal static class ApprovalVisibility
{
    public static IQueryable<ApprovalRequest> In(IQueryable<ApprovalRequest> requests, ApprovalBox box, ApprovalCaller caller)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(caller);

        return box switch
        {
            ApprovalBox.Waiting => requests.Where(WaitingFor(caller)),
            ApprovalBox.Mine => requests.Where(AskedOrSubmittedBy(caller.ObjectId)),
            ApprovalBox.Decided => requests.Where(DecidedBy(caller.ObjectId)),
            ApprovalBox.All => requests,
            _ => throw new ArgumentOutOfRangeException(nameof(box), box, "There is no such list of approval requests."),
        };
    }

    public static Expression<Func<ApprovalRequest, bool>> WaitingFor(ApprovalCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var person = caller.ObjectId;
        var roles = caller.Roles.Select(role => (ErpRole?)role).ToList();

        return request => request.State == ApprovalState.Submitted
            && request.RequestedFor != person
            && request.CreatedBy != person
            && request.Steps.Any(step => step.State == ApprovalStepState.Waiting
                && ((step.ApproverKind == ApproverKind.Person && step.ApproverId == person)
                    || (step.ApproverKind == ApproverKind.Role && roles.Contains(step.ApproverRole))));
    }

    public static Expression<Func<ApprovalRequest, bool>> AskedOrSubmittedBy(Guid person) =>
        request => request.RequestedFor == person || request.CreatedBy == person;

    public static Expression<Func<ApprovalRequest, bool>> DecidedBy(Guid person) =>
        request => request.Steps.Any(step => step.DecidedBy == person);

    public static Expression<Func<ApprovalRequest, bool>> ClosedVisibleTo(ApprovalCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var person = caller.ObjectId;

        return caller.SeesAll
            ? request => true
            : request => request.RequestedFor == person || request.CreatedBy == person || request.Steps.Any(step => step.DecidedBy == person);
    }

    public static bool IsVisibleTo(ApprovalRequest request, ApprovalCaller caller)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        return caller.SeesAll
            || ApprovalRouting.IsOwn(request, caller.ObjectId)
            || request.Steps.Any(step => step.DecidedBy == caller.ObjectId || ApprovalRouting.WaitsFor(step, caller));
    }
}
