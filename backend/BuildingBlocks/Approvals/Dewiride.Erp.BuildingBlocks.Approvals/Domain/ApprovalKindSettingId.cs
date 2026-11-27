using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Domain;

public readonly record struct ApprovalKindSettingId(Guid Value) : IStronglyTypedId<ApprovalKindSettingId>
{
    public static ApprovalKindSettingId Create() => new(Guid.CreateVersion7());

    public static ApprovalKindSettingId From(Guid value) => new(value);
}
