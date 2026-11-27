using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Endpoints.Responses;

/// <summary>An approval request as a list shows it.</summary>
/// <param name="Id">Identifier of the request.</param>
/// <param name="Kind">Key of the kind of request.</param>
/// <param name="KindTitle">What a request of the kind is called, such as Bill approval; the key when no release declares the kind any more.</param>
/// <param name="SubjectType">Change key of the kind of record the request approves, as the audit trail names it.</param>
/// <param name="SubjectId">Identifier of the record the request approves.</param>
/// <param name="SubjectName">Name of the record as its module gives it; null when its module names none.</param>
/// <param name="RequestedForId">Microsoft Entra object id of the person the request is for, whose manager decides a manager step.</param>
/// <param name="RequestedForName">Name the ERP shows for that person; null when it knows none.</param>
/// <param name="SubmittedById">Microsoft Entra object id of the person who submitted the request, or the system's id when a job submitted it.</param>
/// <param name="SubmittedByName">Name the ERP shows for that person; null when it knows none, as for the system.</param>
/// <param name="SubmittedAt">When the request was submitted.</param>
/// <param name="State">Where the request stands: submitted while it waits, then approved, returned (sent back), rejected or withdrawn.</param>
/// <param name="WaitingStepTitle">Title of the step the request waits at; null once the request is closed.</param>
/// <param name="WaitingForKind">Whether the waiting step waits for one person or for every holder of a role; null once the request is closed.</param>
/// <param name="WaitingForId">Microsoft Entra object id of the person the waiting step waits for; null when it waits for a role or the request is closed.</param>
/// <param name="WaitingForName">Name the ERP shows for that person; null when the step waits for a role, the request is closed or the ERP knows no name.</param>
/// <param name="WaitingForRole">Role whose holders decide the waiting step; null when it waits for one person or the request is closed.</param>
/// <param name="DueAt">First instant the waiting step is late, midnight in India after its last working day; null once the request is closed.</param>
/// <param name="Late">Whether the waiting step is past its due time.</param>
/// <param name="ClosedAt">When the request was approved, sent back, rejected or withdrawn; null while it waits.</param>
internal sealed record ApprovalRequestSummaryResponse(
    Guid Id,
    string Kind,
    string KindTitle,
    string SubjectType,
    Guid SubjectId,
    string? SubjectName,
    Guid RequestedForId,
    string? RequestedForName,
    Guid SubmittedById,
    string? SubmittedByName,
    DateTimeOffset SubmittedAt,
    ApprovalState State,
    string? WaitingStepTitle,
    ApproverKind? WaitingForKind,
    Guid? WaitingForId,
    string? WaitingForName,
    ErpRole? WaitingForRole,
    DateTimeOffset? DueAt,
    bool Late,
    DateTimeOffset? ClosedAt);
