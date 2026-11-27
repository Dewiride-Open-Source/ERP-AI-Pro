using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Approvals.Submitting;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample.Commands;

public sealed record SubmitSampleClaimCommand(
    SampleClaimId ClaimId,
    string Kind,
    [property: Required][property: StringLength(SampleClaim.TitleMaxLength)] string Title,
    Guid RequestedFor,
    bool ReceiptAttached) : ICommand<ApprovalSubmitted>;
