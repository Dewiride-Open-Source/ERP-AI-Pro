using Dewiride.Erp.BuildingBlocks.Application.Commands;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample.Commands;

public sealed record CancelSampleClaimCommand(SampleClaimId ClaimId, string Kind) : ICommand<SampleClaimState>;
