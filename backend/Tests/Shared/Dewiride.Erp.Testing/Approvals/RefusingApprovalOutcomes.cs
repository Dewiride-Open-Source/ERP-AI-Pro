using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Outcomes;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Testing.Approvals;

// The record of a refused kind behaves as a record that changed since it was submitted: it refuses to be approved, which leaves its
// request waiting and is the decider's answer, and accepts being sent back, rejected or withdrawn, as every module's record must.
public sealed class RefusingApprovalOutcomes : IApprovalOutcomeHandler
{
    public const string RefusedCode = "test.approvals.outcome-refused";

    public const string RefusedMessage = "The test record cannot be approved as it stands; send it back or reject it instead.";

    public Task<Result> HandleAsync(ApprovalOutcome outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return Task.FromResult(outcome.State == ApprovalState.Approved ? Result.Fail(Error.Failure(RefusedCode, RefusedMessage)) : Result.Success());
    }
}
