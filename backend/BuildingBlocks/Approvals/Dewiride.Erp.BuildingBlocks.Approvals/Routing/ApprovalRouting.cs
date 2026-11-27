using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Routing;

// Every step is settled when the request is submitted, so the person who asks sees the whole route at once and each list is one
// query. A manager step goes to the first of the asker's managers on the day of submission, nearest first, who is active, has an
// account and neither asked for nor submitted the request, and to the administrators when nobody is left; a role step goes to every
// holder of its role, read from the roles a person holds when they decide, because the administrators' role comes from Microsoft
// Entra and the ERP cannot list its holders. Nobody decides a request they asked for or submitted, administrators included, and a
// person who may decide any request decides one that waits for somebody else in that person's place. The reporting lines are
// another module's, asked for only when a step goes to a manager, so a kind decided by roles alone is submitted whichever modules
// the host composes.
internal sealed class ApprovalRouting(IServiceProvider services)
{
    public async Task<IReadOnlyList<ApprovalStepPlan>> PlanAsync(ApprovalKind kind, int workingDays, Guid requestedFor, Guid submittedBy, DateOnly on, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(kind);

        IReadOnlyList<ReportingManager>? managers = null;
        var plans = new List<ApprovalStepPlan>(kind.Steps.Count);
        foreach (var step in kind.Steps)
        {
            if (step.Rule == ApprovalStepRule.Manager)
            {
                managers ??= await ReportingManagers().ManagersOfAsync(requestedFor, on, cancellationToken).ConfigureAwait(false);
                var manager = managers.FirstOrDefault(candidate => candidate is { IsActive: true, ObjectId: { } id } && id != requestedFor && id != submittedBy);
                plans.Add(manager?.ObjectId is { } approver
                    ? ApprovalStepPlan.ForPerson(step, approver, workingDays)
                    : ApprovalStepPlan.ForRole(step, ErpRole.Administrator, workingDays));
            }
            else
            {
                plans.Add(ApprovalStepPlan.ForRole(step, step.Role ?? throw new InvalidOperationException($"The step '{step.Title}' of {kind.Key} names no role."), workingDays));
            }
        }

        return plans;
    }

    public static bool IsOwn(ApprovalRequest request, Guid person)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.RequestedFor == person || request.CreatedBy == person;
    }

    public static bool WaitsFor(ApprovalStep step, ApprovalCaller caller)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(caller);

        return step.State == ApprovalStepState.Waiting && step.ApproverKind switch
        {
            ApproverKind.Person => step.ApproverId == caller.ObjectId,
            _ => step.ApproverRole is { } role && caller.Roles.Contains(role),
        };
    }

    public static ApprovalEligibility EligibilityOf(ApprovalRequest request, ApprovalCaller caller)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        if (IsOwn(request, caller.ObjectId))
        {
            return ApprovalEligibility.OwnRequest;
        }

        if (request.WaitingStep is not { } step)
        {
            return ApprovalEligibility.None;
        }

        if (WaitsFor(step, caller))
        {
            return ApprovalEligibility.Approver;
        }

        return caller.DecidesAny ? ApprovalEligibility.InPlaceOfApprover : ApprovalEligibility.None;
    }

    private IReportingManagers ReportingManagers() =>
        services.GetService<IReportingManagers>()
        ?? throw new InvalidOperationException($"No {nameof(IReportingManagers)} is registered, so a step decided by the asker's manager cannot be routed; the module that holds the people's reporting lines answers it.");
}
