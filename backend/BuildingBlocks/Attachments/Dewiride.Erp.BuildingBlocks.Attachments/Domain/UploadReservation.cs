namespace Dewiride.Erp.BuildingBlocks.Attachments.Domain;

// Exists only while its content id names a blob that no StoredContent row references yet: the transaction that records the
// content deletes it, and the sweeper removes the blob of any reservation that outlives UploadReservationLifetime.
internal sealed class UploadReservation
{
    private UploadReservation()
    {
    }

    public UploadReservation(StoredContentId contentId, DateTimeOffset reservedAt, Guid reservedBy)
    {
        ContentId = contentId;
        ReservedAt = reservedAt;
        ReservedBy = reservedBy;
    }

    public StoredContentId ContentId { get; private set; }

    public DateTimeOffset ReservedAt { get; private set; }

    public Guid ReservedBy { get; private set; }
}
