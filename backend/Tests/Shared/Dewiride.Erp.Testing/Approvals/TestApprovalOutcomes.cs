using Dewiride.Erp.BuildingBlocks.Approvals.Outcomes;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Testing.Approvals;

// A test kind approves no stored record, so a decision on one of its requests moves nothing and every outcome is accepted.
public sealed class TestApprovalOutcomes : IApprovalOutcomeHandler
{
    public Task<Result> HandleAsync(ApprovalOutcome outcome, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
}
