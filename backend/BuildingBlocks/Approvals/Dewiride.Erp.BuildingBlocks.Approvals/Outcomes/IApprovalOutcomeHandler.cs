using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Outcomes;

// The module that declares a kind moves its record when a request of the kind is approved, sent back, rejected or withdrawn on the
// Approvals page. Its handler is a sealed class of the module's Integration folder, created for each decision and never registered,
// and it runs inside the decision's unit of work on the module's own context, under the request's lock: it translates the outcome
// into the module's own command, whose unit of work joins that transaction, and never saves, calls an outside service or asks
// for an approval itself. It checks none of the decider's permissions, because the building block settled who may decide. A failed
// result rolls the decision back and is the decider's answer, and only Approved may be refused: a record that changed since it was
// submitted must still be sent back, rejected or withdrawn, so any other outcome refused for another reason than a conflict or an
// unavailable dependency, which the decider can try again, throws.
public interface IApprovalOutcomeHandler
{
    Task<Result> HandleAsync(ApprovalOutcome outcome, CancellationToken cancellationToken);
}
