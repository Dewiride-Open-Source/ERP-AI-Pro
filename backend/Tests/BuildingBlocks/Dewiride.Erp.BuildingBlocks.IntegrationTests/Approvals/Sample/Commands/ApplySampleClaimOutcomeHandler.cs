using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample.Commands;

// A claim without its receipt is never approved, so its approval is refused while sending it back, rejecting and withdrawing it
// still go through, as an outcome handler must let them.
public sealed class ApplySampleClaimOutcomeHandler(SampleDbContext context) : ICommandHandler<ApplySampleClaimOutcomeCommand, SampleClaimState>
{
    public const string ReceiptMissingCode = "test.approvals.receipt-missing";

    public async Task<Result<SampleClaimState>> HandleAsync(ApplySampleClaimOutcomeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var claim = await context.Claims.SingleAsync(c => c.Id == command.ClaimId, cancellationToken);
        if (command.Outcome == ApprovalState.Approved && !claim.ReceiptAttached)
        {
            return Error.Failure(ReceiptMissingCode, "A claim is approved with its receipt only.");
        }

        claim.MoveTo(command.Outcome switch
        {
            ApprovalState.Approved => SampleClaimState.Approved,
            ApprovalState.Returned => SampleClaimState.SentBack,
            ApprovalState.Rejected => SampleClaimState.Rejected,
            _ => SampleClaimState.Draft,
        });

        return claim.State;
    }
}
