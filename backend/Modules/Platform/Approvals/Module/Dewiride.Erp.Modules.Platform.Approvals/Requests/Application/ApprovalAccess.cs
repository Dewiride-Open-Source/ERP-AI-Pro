using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Application;

// What the caller's request lets them do with approval requests, read by the endpoint from the roles and permissions the request
// carries; a handler adds the person the request acts for, the actor every change record of a decision names.
internal sealed record ApprovalAccess(IReadOnlySet<ErpRole> Roles, bool SeesAll, bool DecidesAny)
{
    public ApprovalCaller For(Guid objectId) => new(objectId, Roles, SeesAll, DecidesAny);
}
