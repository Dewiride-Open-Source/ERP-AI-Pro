using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;

namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Commands.DecideApprovalRequest;

internal sealed record DecideApprovalRequestCommand(ApprovalRequestId Id, ApprovalDecision Decision, string? Comment, string Version, ApprovalAccess Access) : ICommand<ApprovalRequestDetails>;
