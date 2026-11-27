using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Approvals.Submitting;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample.Commands;

// The waiting request is withdrawn first, before the claim is read, so a decision being saved is waited for and the claim is
// cancelled as that decision left it.
public sealed class CancelSampleClaimHandler(SampleDbContext context, IApprovals approvals) : ICommandHandler<CancelSampleClaimCommand, SampleClaimState>
{
    public async Task<Result<SampleClaimState>> HandleAsync(CancelSampleClaimCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var withdrawn = await approvals.WithdrawAsync(context, command.Kind, command.ClaimId.Value, cancellationToken);
        if (withdrawn.IsFailure)
        {
            return withdrawn.Error!;
        }

        var claim = await context.Claims.SingleAsync(c => c.Id == command.ClaimId, cancellationToken);
        claim.MoveTo(SampleClaimState.Cancelled);

        return claim.State;
    }
}
