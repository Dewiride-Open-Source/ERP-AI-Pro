using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Approvals.UnitTests.Requests.Application;

internal sealed class RecordingApprovalDecisions : IApprovalDecisions
{
    public List<string> Calls { get; } = [];

    public ApprovalRequestId? Id { get; private set; }

    public ApprovalDecision? Decision { get; private set; }

    public string? Comment { get; private set; }

    public byte[]? Version { get; private set; }

    public ApprovalCaller? Caller { get; private set; }

    public Task<Result<ApprovalRequestDetails>> DecideAsync(ApprovalRequestId id, ApprovalDecision decision, string? comment, byte[] version, ApprovalCaller caller, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(DecideAsync));
        Id = id;
        Decision = decision;
        Comment = comment;
        Version = version;
        Caller = caller;

        return Task.FromResult<Result<ApprovalRequestDetails>>(ApprovalSamples.Details);
    }

    public Task<Result<ApprovalRequestDetails>> WithdrawAsync(ApprovalRequestId id, string? comment, byte[] version, ApprovalCaller caller, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(WithdrawAsync));
        Id = id;
        Comment = comment;
        Version = version;
        Caller = caller;

        return Task.FromResult<Result<ApprovalRequestDetails>>(ApprovalSamples.Details);
    }
}
