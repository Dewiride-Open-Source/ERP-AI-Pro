using Dewiride.Erp.BuildingBlocks.Approvals.Domain;

namespace Dewiride.Erp.Modules.Platform.Approvals.Requests.Endpoints.Responses;

/// <summary>An earlier request of the same kind for the same record.</summary>
/// <param name="Id">Identifier of the earlier request.</param>
/// <param name="State">Where the earlier request stands.</param>
/// <param name="SubmittedAt">When the earlier request was submitted.</param>
/// <param name="ClosedAt">When the earlier request was approved, sent back, rejected or withdrawn; null while it waits.</param>
internal sealed record EarlierApprovalRequestResponse(Guid Id, ApprovalState State, DateTimeOffset SubmittedAt, DateTimeOffset? ClosedAt);
