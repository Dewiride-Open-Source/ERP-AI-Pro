using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Definitions;

// A step is decided by the manager of the person the request is for, as their position names that manager on the day it is
// submitted, or by any holder of a built-in role, so the ERP settles who decides from what it already knows of people.
public sealed record ApprovalStepDefinition
{
    public const int TitleMaxLength = 60;

    private ApprovalStepDefinition(string title, ApprovalStepRule rule, ErpRole? role)
    {
        Title = title;
        Rule = rule;
        Role = role;
    }

    public string Title { get; }

    public ApprovalStepRule Rule { get; }

    public ErpRole? Role { get; }

    public static ApprovalStepDefinition Manager(string title) => new(title, ApprovalStepRule.Manager, null);

    public static ApprovalStepDefinition HeldBy(string title, ErpRole role) => new(title, ApprovalStepRule.Role, role);
}
