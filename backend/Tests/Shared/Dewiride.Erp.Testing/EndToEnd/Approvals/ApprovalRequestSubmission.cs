namespace Dewiride.Erp.Testing.EndToEnd.Approvals;

public sealed record ApprovalRequestSubmission(string? Kind, Guid? RequestedFor, int? SubmittedWorkingDaysAgo, Guid? SubjectId);
