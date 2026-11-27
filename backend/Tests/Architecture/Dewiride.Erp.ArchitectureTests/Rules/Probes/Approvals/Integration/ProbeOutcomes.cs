using Dewiride.Erp.BuildingBlocks.Approvals.Outcomes;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.ArchitectureTests.Rules.Probes.Approvals.Integration;

// Outcome handlers placed as a module's Integration folder places them, one keeping the rules, one that is not sealed and one
// asking for a service the API host does not compose.
public static class ProbeOutcomes
{
    public interface IClaimLedger
    {
        bool Holds(Guid claimId);
    }

    public sealed class ClaimOutcomes : IApprovalOutcomeHandler
    {
        public Task<Result> HandleAsync(ApprovalOutcome outcome, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
    }

    public class OpenClaimOutcomes : IApprovalOutcomeHandler
    {
        public Task<Result> HandleAsync(ApprovalOutcome outcome, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
    }

    public sealed class LedgerClaimOutcomes(IClaimLedger ledger) : IApprovalOutcomeHandler
    {
        public Task<Result> HandleAsync(ApprovalOutcome outcome, CancellationToken cancellationToken) =>
            Task.FromResult(ledger.Holds(outcome?.SubjectId ?? Guid.Empty) ? Result.Success() : Result.Fail(Error.NotFound("probe.claim-unknown", "The claim is not in the ledger.")));
    }
}
