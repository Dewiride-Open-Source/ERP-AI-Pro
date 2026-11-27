using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;

namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Application.Commands.WithdrawApprovalRequest;

internal sealed record WithdrawApprovalRequestCommand(ApprovalRequestId Id, string? Comment, string Version, ApprovalAccess Access) : ICommand<ApprovalRequestDetails>;
