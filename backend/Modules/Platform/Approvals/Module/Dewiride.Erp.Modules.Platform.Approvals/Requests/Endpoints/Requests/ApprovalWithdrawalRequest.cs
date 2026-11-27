using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;

namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Endpoints.Requests;

public sealed record ApprovalWithdrawalRequest(
    [property: Description("Version of the request the withdrawal was made from, as the API last returned it.")]
    [property: Required]
    [property: StringLength(RecordVersion.InputMaxLength)]
    string? Version,
    [property: Description("Why the request is withdrawn; optional, at most 1,000 characters, kept without the spaces at either end.")]
    [property: StringLength(ApprovalStep.CommentMaxLength)]
    string? Comment);
