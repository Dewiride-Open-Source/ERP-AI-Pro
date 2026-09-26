using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Domain;

public readonly record struct AttachmentId(Guid Value) : IStronglyTypedId<AttachmentId>
{
    public static AttachmentId Create() => new(Guid.CreateVersion7());

    public static AttachmentId From(Guid value) => new(value);
}
