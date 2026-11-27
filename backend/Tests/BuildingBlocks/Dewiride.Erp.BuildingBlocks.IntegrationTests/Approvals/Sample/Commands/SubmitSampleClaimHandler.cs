using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Submitting;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample.Commands;

// The claim's request is asked for last, after the claim is made, and while its kind needs no approval the claim is approved at
// once.
public sealed class SubmitSampleClaimHandler(SampleDbContext context, IApprovals approvals) : ICommandHandler<SubmitSampleClaimCommand, ApprovalSubmitted>
{
    public async Task<Result<ApprovalSubmitted>> HandleAsync(SubmitSampleClaimCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var claim = SampleClaim.Draft(command.ClaimId, command.Title, command.RequestedFor, command.ReceiptAttached);
        context.Claims.Add(claim);
        var submitted = await approvals.SubmitAsync(context, command.Kind, command.ClaimId.Value, command.RequestedFor, cancellationToken);
        if (submitted.IsFailure)
        {
            return submitted.Error!;
        }

        claim.MoveTo(submitted.Value.State == ApprovalState.Approved ? SampleClaimState.Approved : SampleClaimState.Waiting);

        return submitted.Value;
    }
}
