using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Domain;

// One row per download actually served: the record of who read which file and when (CLAUDE.md §9).
internal sealed class DownloadRedemption : Entity<DownloadRedemptionId>
{
    private DownloadRedemption(DownloadRedemptionId id)
        : base(id)
    {
    }

    public DownloadRedemption(DownloadRedemptionId id, DownloadLinkId linkId, AttachmentId attachmentId, Guid actorId, DateTimeOffset redeemedAt)
        : base(id)
    {
        LinkId = linkId;
        AttachmentId = attachmentId;
        ActorId = actorId;
        RedeemedAt = redeemedAt;
    }

    public DownloadLinkId LinkId { get; private set; }

    public AttachmentId AttachmentId { get; private set; }

    public Guid ActorId { get; private set; }

    public DateTimeOffset RedeemedAt { get; private set; }
}
