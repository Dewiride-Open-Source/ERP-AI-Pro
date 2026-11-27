using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;

namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Queries.CountApprovalRequests;

internal sealed record CountApprovalRequestsQuery(ApprovalAccess Access) : IQuery<ApprovalCounts>;
