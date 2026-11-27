using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Domain;

public readonly record struct ApprovalStepId(Guid Value) : IStronglyTypedId<ApprovalStepId>
{
    public static ApprovalStepId Create() => new(Guid.CreateVersion7());

    public static ApprovalStepId From(Guid value) => new(value);
}
