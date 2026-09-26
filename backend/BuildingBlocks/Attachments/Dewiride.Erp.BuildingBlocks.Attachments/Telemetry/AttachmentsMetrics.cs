using System.Diagnostics.Metrics;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Telemetry;

internal sealed class AttachmentsMetrics
{
    public const string MeterName = "Dewiride.Erp.Attachments";

    public const string OutcomeTag = "erp.attachments.outcome";

    public const string ErrorCodeTag = "erp.error.code";

    private readonly Counter<long> _uploads;
    private readonly Histogram<long> _uploadSize;
    private readonly Counter<long> _downloads;
    private readonly Counter<long> _sweptReservations;
    private readonly Counter<long> _purgedLinks;

    public AttachmentsMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(MeterName);
        _uploads = meter.CreateCounter<long>("erp.attachments.uploads", "{upload}", "Uploads by outcome: stored, deduplicated or rejected.");
        _uploadSize = meter.CreateHistogram<long>("erp.attachments.upload.size", "By", "Size of accepted uploads.");
        _downloads = meter.CreateCounter<long>("erp.attachments.downloads", "{download}", "Download link redemptions by outcome: served or refused.");
        _sweptReservations = meter.CreateCounter<long>("erp.attachments.reservations.swept", "{reservation}", "Abandoned upload reservations whose blobs were removed.");
        _purgedLinks = meter.CreateCounter<long>("erp.attachments.links.purged", "{link}", "Download links removed after expiring unused.");
    }

    public void UploadAccepted(long sizeBytes, bool deduplicated)
    {
        _uploads.Add(1, new KeyValuePair<string, object?>(OutcomeTag, deduplicated ? "deduplicated" : "stored"));
        _uploadSize.Record(sizeBytes);
    }

    public void UploadRejected(string errorCode) =>
        _uploads.Add(1, new KeyValuePair<string, object?>(OutcomeTag, "rejected"), new KeyValuePair<string, object?>(ErrorCodeTag, errorCode));

    public void DownloadServed() => _downloads.Add(1, new KeyValuePair<string, object?>(OutcomeTag, "served"));

    public void DownloadRefused() => _downloads.Add(1, new KeyValuePair<string, object?>(OutcomeTag, "refused"));

    public void ReservationsSwept(int count) => _sweptReservations.Add(count);

    public void LinksPurged(int count) => _purgedLinks.Add(count);
}
