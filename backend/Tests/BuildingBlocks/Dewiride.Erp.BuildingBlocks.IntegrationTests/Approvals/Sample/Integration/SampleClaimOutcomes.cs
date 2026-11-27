using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Approvals.Outcomes;
using Dewiride.Erp.BuildingBlocks.Approvals.Submitting;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample.Commands;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample.Integration;

// The claim moves through the sample's own command, whose unit of work joins the decision's transaction, unless the test's script
// makes the handler misbehave in one of the ways the building block refuses.
public sealed class SampleClaimOutcomes(
    ICommandHandler<ApplySampleClaimOutcomeCommand, SampleClaimState> apply,
    SampleClaimScript script,
    IApprovals approvals,
    SampleDbContext context) : IApprovalOutcomeHandler
{
    public const string RefusedCode = "test.approvals.outcome-refused";

    public const string BusyCode = "test.approvals.outcome-busy";

    public const string UnavailableCode = "test.approvals.outcome-unavailable";

    public async Task<Result> HandleAsync(ApprovalOutcome outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        script.Received.Enqueue(outcome);
        await script.WaitWhileHeldAsync(cancellationToken);
        switch (script.Misbehaviour)
        {
            case SampleClaimMisbehaviour.AskForApproval:
                await approvals.SubmitAsync(context, outcome.Kind, outcome.SubjectId, outcome.DecidedBy, cancellationToken);
                break;
            case SampleClaimMisbehaviour.Withdraw:
                await approvals.WithdrawAsync(context, outcome.Kind, outcome.SubjectId, cancellationToken);
                break;
            case SampleClaimMisbehaviour.RefuseEveryOutcome:
                return Error.Failure(RefusedCode, "The sample refuses every outcome.");
            case SampleClaimMisbehaviour.ReportBusy:
                return Error.Conflict(BusyCode, "The sample's claim is being changed by someone else.");
            case SampleClaimMisbehaviour.ReportUnavailable:
                return Error.Unavailable(UnavailableCode, "The sample cannot reach what it needs.");
        }

        var applied = await apply.HandleAsync(new ApplySampleClaimOutcomeCommand(SampleClaimId.From(outcome.SubjectId), outcome.State), cancellationToken);

        return applied.IsFailure ? Result.Fail(applied.Error!) : Result.Success();
    }
}
