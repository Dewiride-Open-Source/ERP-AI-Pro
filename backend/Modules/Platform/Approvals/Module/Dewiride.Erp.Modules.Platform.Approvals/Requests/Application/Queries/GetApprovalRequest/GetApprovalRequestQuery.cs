using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;

namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Queries.GetApprovalRequest;

internal sealed record GetApprovalRequestQuery(ApprovalRequestId Id, ApprovalAccess Access) : IQuery<ApprovalRequestDetails>;
