using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;

namespace Dewiride.Erp.ArchitectureTests.Rules.Probes.Approvals;

// Kinds declared as a module of the prefix probe.ledgers declares its own: one keeping every rule of a kind, and one breaking each.
public static class ProbeApprovals
{
    public const string PermissionPrefix = "probe.ledgers.";

    public const string ClaimsKey = "probe.ledgers.claims";

    public static readonly ApprovalKind Keeping = new(
        "probe.ledgers.claim-approval",
        "Claim approval",
        "A probe claim waits for approval before it is paid.",
        ClaimsKey,
        [ApprovalStepDefinition.Manager("Manager")],
        2);

    public static readonly ApprovalKind Stray = Keeping with { Key = "probe.other.claim-approval" };

    public static readonly ApprovalKind Rambling = Keeping with { Description = "A probe claim waits for approval. Then it is paid." };

    public static readonly ApprovalKind Unrecorded = Keeping with { SubjectType = "probe.ledgers.vouchers" };
}
