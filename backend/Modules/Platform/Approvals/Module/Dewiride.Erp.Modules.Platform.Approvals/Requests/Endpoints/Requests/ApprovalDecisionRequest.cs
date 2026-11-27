using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;

namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Endpoints.Requests;

public sealed record ApprovalDecisionRequest(
    [property: Description("Version of the request the decision was made from, as the API last returned it.")]
    [property: Required]
    [property: StringLength(RecordVersion.InputMaxLength)]
    string? Version,
    [property: Description("What needs to change, to send the request back, or why it is rejected, to reject it, both of which need one; a note to approve it, which needs none. At most 1,000 characters, kept without the spaces at either end; a blank one is none.")]
    [property: StringLength(ApprovalStep.CommentMaxLength)]
    string? Comment);
