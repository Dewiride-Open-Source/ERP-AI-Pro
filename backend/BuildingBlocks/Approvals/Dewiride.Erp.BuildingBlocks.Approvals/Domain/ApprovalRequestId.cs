using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Domain;

public readonly record struct ApprovalRequestId(Guid Value) : IStronglyTypedId<ApprovalRequestId>
{
    public static ApprovalRequestId Create() => new(Guid.CreateVersion7());

    public static ApprovalRequestId From(Guid value) => new(value);
}
