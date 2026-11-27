using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample.Commands;

public sealed record ApplySampleClaimOutcomeCommand(SampleClaimId ClaimId, ApprovalState Outcome) : ICommand<SampleClaimState>;
