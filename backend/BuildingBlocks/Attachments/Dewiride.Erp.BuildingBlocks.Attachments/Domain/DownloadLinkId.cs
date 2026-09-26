using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Domain;

internal readonly record struct DownloadLinkId(Guid Value) : IStronglyTypedId<DownloadLinkId>
{
    public static DownloadLinkId Create() => new(Guid.CreateVersion7());

    public static DownloadLinkId From(Guid value) => new(value);
}
