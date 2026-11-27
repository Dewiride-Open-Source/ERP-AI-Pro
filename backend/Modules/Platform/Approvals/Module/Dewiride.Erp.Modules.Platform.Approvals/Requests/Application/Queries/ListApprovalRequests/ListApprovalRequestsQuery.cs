using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;

namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Queries.ListApprovalRequests;

internal sealed record ListApprovalRequestsQuery(ApprovalBox Box, ListRequest List, ApprovalAccess Access) : IQuery<PagedResult<ApprovalRequestSummary>>;
