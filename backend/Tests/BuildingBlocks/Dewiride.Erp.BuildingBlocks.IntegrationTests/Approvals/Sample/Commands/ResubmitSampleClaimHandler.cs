using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Submitting;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample.Commands;

public sealed class ResubmitSampleClaimHandler(SampleDbContext context, IApprovals approvals) : ICommandHandler<ResubmitSampleClaimCommand, ApprovalSubmitted>
{
    public const string NotDraftCode = "test.approvals.claim-not-draft";

    public async Task<Result<ApprovalSubmitted>> HandleAsync(ResubmitSampleClaimCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var claim = await context.Claims.SingleAsync(c => c.Id == command.ClaimId, cancellationToken);
        if (claim.State is not (SampleClaimState.Draft or SampleClaimState.SentBack))
        {
            return Error.Conflict(NotDraftCode, "Only a draft or a claim sent back is sent again.");
        }

        var submitted = await approvals.SubmitAsync(context, command.Kind, command.ClaimId.Value, claim.RequestedFor, cancellationToken);
        if (submitted.IsFailure)
        {
            return submitted.Error!;
        }

        claim.MoveTo(submitted.Value.State == ApprovalState.Approved ? SampleClaimState.Approved : SampleClaimState.Waiting);

        return submitted.Value;
    }
}
