using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Approvals.Submitting;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample.Commands;

public sealed record ResubmitSampleClaimCommand(SampleClaimId ClaimId, string Kind) : ICommand<ApprovalSubmitted>;
