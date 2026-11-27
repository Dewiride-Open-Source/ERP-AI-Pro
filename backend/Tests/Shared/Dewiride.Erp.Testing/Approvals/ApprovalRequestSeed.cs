namespace Dewiride.Erp.Testing.Approvals;

// RequestedFor is the Microsoft Entra object id of the person the request is for, whose manager decides a manager step. The record
// is a new one unless the seed names one, and the request is submitted now unless the seed says how many working days ago.
public sealed record ApprovalRequestSeed(string Kind, Guid RequestedFor)
{
    public int SubmittedWorkingDaysAgo { get; init; }

    public Guid SubjectId { get; init; } = Guid.CreateVersion7();
}
