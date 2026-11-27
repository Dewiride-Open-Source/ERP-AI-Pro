using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Endpoints.Responses;

/// <summary>One step of an approval request.</summary>
/// <param name="Order">Position of the step, from 1, in the order the steps are decided.</param>
/// <param name="Title">Title of the step as its kind declared it when the request was submitted, such as Manager or Finance.</param>
/// <param name="Rule">What the kind asks of the step: manager, the manager of the person the request is for, or role, a holder of a role.</param>
/// <param name="State">Where the step stands: pending until the request reaches it, waiting, approved, returned, rejected, or closed when it ended without a decision of its own.</param>
/// <param name="ApproverKind">Whether the step waits for one person or for every holder of a role, as its submission settled.</param>
/// <param name="ApproverId">Microsoft Entra object id of the person the step waits for; null when it waits for a role.</param>
/// <param name="ApproverName">Name the ERP shows for that person; null when the step waits for a role or the ERP knows no name.</param>
/// <param name="ApproverRole">Role whose holders decide the step, administrator when a manager step found no manager to decide; null when it waits for one person.</param>
/// <param name="WorkingDays">Working days the step has from the moment it starts waiting, as the kind gave them when the request was submitted.</param>
/// <param name="StartedAt">When the step began to wait; null while it is pending or when it closed before it was reached.</param>
/// <param name="DueAt">First instant the step is late, midnight in India after its last working day; null until it starts waiting.</param>
/// <param name="Late">Whether the step waits past its due time.</param>
/// <param name="DecidedById">Microsoft Entra object id of the person who decided the step; null until it is decided, and for a step that closed without a decision.</param>
/// <param name="DecidedByName">Name the ERP shows for that person; null when nobody decided the step or the ERP knows no name.</param>
/// <param name="DecidedAt">When the step was decided; null until it is.</param>
/// <param name="InPlaceOfApprover">Whether its decider decided in place of the person or role it waited for.</param>
/// <param name="Comment">The note or reason its decider gave, or the reason given to withdraw the request while the step waited; null when none was given.</param>
internal sealed record ApprovalStepResponse(
    int Order,
    string Title,
    ApprovalStepRule Rule,
    ApprovalStepState State,
    ApproverKind ApproverKind,
    Guid? ApproverId,
    string? ApproverName,
    ErpRole? ApproverRole,
    int WorkingDays,
    DateTimeOffset? StartedAt,
    DateTimeOffset? DueAt,
    bool Late,
    Guid? DecidedById,
    string? DecidedByName,
    DateTimeOffset? DecidedAt,
    bool InPlaceOfApprover,
    string? Comment);
