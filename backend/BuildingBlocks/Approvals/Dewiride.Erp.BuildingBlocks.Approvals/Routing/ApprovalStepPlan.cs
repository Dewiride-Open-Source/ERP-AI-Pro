using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Routing;

// Who one step of a request waits for, settled when the request is submitted, with the working days the step has, which the kind's
// setting gave at that moment, so a decision never reads the setting again.
internal sealed record ApprovalStepPlan(string Title, ApprovalStepRule Rule, ApproverKind ApproverKind, Guid? ApproverId, ErpRole? ApproverRole, int WorkingDays)
{
    public static ApprovalStepPlan ForPerson(ApprovalStepDefinition step, Guid person, int workingDays)
    {
        ArgumentNullException.ThrowIfNull(step);

        return new ApprovalStepPlan(step.Title, step.Rule, ApproverKind.Person, person, null, workingDays);
    }

    public static ApprovalStepPlan ForRole(ApprovalStepDefinition step, ErpRole role, int workingDays)
    {
        ArgumentNullException.ThrowIfNull(step);

        return new ApprovalStepPlan(step.Title, step.Rule, ApproverKind.Role, null, role, workingDays);
    }
}
