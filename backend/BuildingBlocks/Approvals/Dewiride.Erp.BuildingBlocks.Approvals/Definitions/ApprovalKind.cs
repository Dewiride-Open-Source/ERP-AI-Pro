using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Definitions;

// The key names the kind in its requests' rows, so it never follows a class name: renaming one leaves its requests under a key no
// release declares. The subject type is the change key of the record the kind approves, so a request names its record as the audit
// trail does, and the working days are the time each step has, which an administrator may change for the kind. A step never names
// Employee, which every person holds, because a step anybody may decide approves nothing.
public sealed record ApprovalKind(string Key, string Title, string Description, string SubjectType, IReadOnlyList<ApprovalStepDefinition> Steps, int WorkingDaysToDecide)
{
    public const int KeyMaxLength = ApprovalKindKeys.MaxLength;

    public const int TitleMaxLength = 100;

    public const int DescriptionMaxLength = 400;

    public const int SubjectTypeMaxLength = ChangeRecordMapping.KeyMaxLength;

    public const int MaxSteps = 5;

    public const int MinWorkingDays = 1;

    public const int MaxWorkingDays = 30;

    internal void EnsureValid()
    {
        if (!ApprovalKindKeys.IsWellFormed(Key))
        {
            throw new ArgumentException($"The approval kind key '{Key}' must be three lower-case kebab segments, <domain>.<module>.<kind>, of at most {KeyMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(Title) || Title.Length > TitleMaxLength)
        {
            throw new ArgumentException($"The approval kind {Key} needs a title of 1 to {TitleMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(Description) || Description.Length > DescriptionMaxLength)
        {
            throw new ArgumentException($"The approval kind {Key} needs a description of 1 to {DescriptionMaxLength} characters.");
        }

        if (!ChangeRecordConventions.IsWellFormedKey(SubjectType))
        {
            throw new ArgumentException($"The approval kind {Key} names '{SubjectType}' as the record it approves; name that record's change key, three lower-case kebab segments of at most {SubjectTypeMaxLength} characters.");
        }

        if (WorkingDaysToDecide is < MinWorkingDays or > MaxWorkingDays)
        {
            throw new ArgumentException($"The approval kind {Key} gives each step {WorkingDaysToDecide} working days; give it {MinWorkingDays} to {MaxWorkingDays}.");
        }

        if (Steps is null || Steps.Count is 0 or > MaxSteps)
        {
            throw new ArgumentException($"The approval kind {Key} needs 1 to {MaxSteps} steps.");
        }

        var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in Steps)
        {
            EnsureValid(step, titles);
        }
    }

    private void EnsureValid(ApprovalStepDefinition? step, HashSet<string> titles)
    {
        if (step is null)
        {
            throw new ArgumentException($"The approval kind {Key} lists a step that is null.");
        }

        if (string.IsNullOrWhiteSpace(step.Title) || step.Title.Length > ApprovalStepDefinition.TitleMaxLength)
        {
            throw new ArgumentException($"Every step of the approval kind {Key} needs a title of 1 to {ApprovalStepDefinition.TitleMaxLength} characters.");
        }

        if (!titles.Add(step.Title))
        {
            throw new ArgumentException($"The approval kind {Key} has more than one step titled '{step.Title}'.");
        }

        if (step.Rule == ApprovalStepRule.Role && (step.Role is not { } role || role == ErpRole.Employee || !Enum.IsDefined(role)))
        {
            throw new ArgumentException($"The step '{step.Title}' of the approval kind {Key} must name {ErpRole.Finance}, {ErpRole.Hr}, {ErpRole.Manager} or {ErpRole.Administrator}.");
        }
    }
}
