namespace Dewiride.Erp.Testing.EndToEnd.Approvals;

// RequestId is null when the administrators switched the kind off, so its record went through without a request.
public sealed record ApprovalRequestSubmissionResponse(Guid? RequestId);
