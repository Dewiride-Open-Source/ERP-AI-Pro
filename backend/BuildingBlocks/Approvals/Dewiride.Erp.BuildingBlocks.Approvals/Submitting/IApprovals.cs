using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Submitting;

// The calls a module makes in its own commands, on its own context, which declares `public DbSet<ApprovalRequest> ApprovalRequests
// => Set<ApprovalRequest>();` and `public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();`. SubmitAsync comes last in the
// command that changes the record, after the command's own checks, so the request commits with the record or not at all; while
// administrators have switched approval off for the kind it answers Approved without a request, and the command applies the
// approval itself. WithdrawAsync comes first in the command that cancels or deletes the record, before that command reads its record
// or takes a lock of its own: it waits for a decision being saved, closes the record's waiting request without calling the outcome
// handler, because the command moves the record itself, and succeeds when none waits. Neither runs while a decision does, and a
// record withdrawn and submitted again needs two units of work. StatusOfAsync answers the record's latest request, null when none.
public interface IApprovals
{
    Task<Result<ApprovalSubmitted>> SubmitAsync(ModuleDbContext context, string kind, Guid subjectId, Guid requestedFor, CancellationToken cancellationToken);

    Task<Result> WithdrawAsync(ModuleDbContext context, string kind, Guid subjectId, CancellationToken cancellationToken);

    Task<ApprovalStatus?> StatusOfAsync(ModuleDbContext context, string kind, Guid subjectId, CancellationToken cancellationToken);
}
