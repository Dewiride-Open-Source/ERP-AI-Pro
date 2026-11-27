using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.Modules.Platform.Approvals.Kinds.Endpoints.Responses;

/// <summary>One step of a kind of request.</summary>
/// <param name="Order">Position of the step, from 1, in the order the steps are decided.</param>
/// <param name="Title">Title of the step, such as Manager or Finance.</param>
/// <param name="Rule">Who decides the step: manager, the manager of the person the request is for on the day it is submitted, or the administrators when there is none; role, any holder of the step's role.</param>
/// <param name="Role">Role whose holders decide the step; null for a manager step.</param>
internal sealed record ApprovalKindStepResponse(int Order, string Title, ApprovalStepRule Rule, ErpRole? Role);
