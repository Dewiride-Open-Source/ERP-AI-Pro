using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Domain;

internal readonly record struct DownloadRedemptionId(Guid Value) : IStronglyTypedId<DownloadRedemptionId>
{
    public static DownloadRedemptionId Create() => new(Guid.CreateVersion7());

    public static DownloadRedemptionId From(Guid value) => new(value);
}
