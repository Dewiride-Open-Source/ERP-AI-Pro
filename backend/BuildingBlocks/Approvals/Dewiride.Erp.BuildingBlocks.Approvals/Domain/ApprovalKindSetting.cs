using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Domain;

// The administrators' choice for one kind: whether its records need approval, and the working days each step of its requests has.
// A kind without a setting needs approval and gives each step the days it declares. A change applies to the requests submitted
// after it, because a request takes its steps' days when it is submitted.
internal sealed class ApprovalKindSetting : AggregateRoot<ApprovalKindSettingId>, IAuditable, IVersioned
{
    private ApprovalKindSetting(ApprovalKindSettingId id)
        : base(id)
    {
    }

    public string Kind { get; private set; } = string.Empty;

    public bool Required { get; private set; }

    public int WorkingDaysToDecide { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static ApprovalKindSetting Create(string kind, bool required, int workingDaysToDecide)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(kind.Length, ApprovalKind.KeyMaxLength, nameof(kind));

        var setting = new ApprovalKindSetting(ApprovalKindSettingId.Create()) { Kind = kind };
        setting.Change(required, workingDaysToDecide);

        return setting;
    }

    public void Change(bool required, int workingDaysToDecide)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(workingDaysToDecide, ApprovalKind.MinWorkingDays);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(workingDaysToDecide, ApprovalKind.MaxWorkingDays);

        Required = required;
        WorkingDaysToDecide = workingDaysToDecide;
    }
}
