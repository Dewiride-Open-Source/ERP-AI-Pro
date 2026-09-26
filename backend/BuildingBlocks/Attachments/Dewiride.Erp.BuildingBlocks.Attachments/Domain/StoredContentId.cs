using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Domain;

internal readonly record struct StoredContentId(Guid Value) : IStronglyTypedId<StoredContentId>
{
    public static StoredContentId Create() => new(Guid.CreateVersion7());

    public static StoredContentId From(Guid value) => new(value);
}
