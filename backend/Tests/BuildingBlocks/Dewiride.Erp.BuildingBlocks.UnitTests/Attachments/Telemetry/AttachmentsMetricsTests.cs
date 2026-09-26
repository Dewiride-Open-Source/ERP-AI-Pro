using System.Diagnostics.Metrics;
using Dewiride.Erp.BuildingBlocks.Attachments.Telemetry;
using Dewiride.Erp.BuildingBlocks.Observability.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Telemetry;

public sealed class AttachmentsMetricsTests : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddMetrics().BuildServiceProvider();

    private IMeterFactory MeterFactory => _services.GetRequiredService<IMeterFactory>();

    [Theory]
    [InlineData(false, "stored")]
    [InlineData(true, "deduplicated")]
    public void UploadAccepted_Upload_CountsItByOutcomeAndRecordsItsSize(bool deduplicated, string outcome)
    {
        using var uploads = Collect("erp.attachments.uploads");
        using var sizes = Collect("erp.attachments.upload.size");
        var metrics = new AttachmentsMetrics(MeterFactory);

        metrics.UploadAccepted(12_345, deduplicated);

        var upload = Assert.Single(uploads.GetMeasurementSnapshot());
        Assert.Equal(1, upload.Value);
        Assert.Equal(outcome, upload.Tags[AttachmentsMetrics.OutcomeTag]);
        var size = Assert.Single(sizes.GetMeasurementSnapshot());
        Assert.Equal(12_345, size.Value);
        Assert.Equal("By", sizes.Instrument!.Unit);
    }

    [Fact]
    public void UploadRejected_ErrorCode_CountsARejectionTaggedWithTheCodeAndRecordsNoSize()
    {
        using var uploads = Collect("erp.attachments.uploads");
        using var sizes = Collect("erp.attachments.upload.size");
        var metrics = new AttachmentsMetrics(MeterFactory);

        metrics.UploadRejected("attachment.too-large");

        var upload = Assert.Single(uploads.GetMeasurementSnapshot());
        Assert.Equal(1, upload.Value);
        Assert.Equal("rejected", upload.Tags[AttachmentsMetrics.OutcomeTag]);
        Assert.Equal("attachment.too-large", upload.Tags[AttachmentsMetrics.ErrorCodeTag]);
        Assert.Empty(sizes.GetMeasurementSnapshot());
    }

    [Fact]
    public void DownloadServedAndRefused_Downloads_AreCountedByOutcome()
    {
        using var downloads = Collect("erp.attachments.downloads");
        var metrics = new AttachmentsMetrics(MeterFactory);

        metrics.DownloadServed();
        metrics.DownloadServed();
        metrics.DownloadRefused();

        Assert.Equal(["served", "served", "refused"], downloads.GetMeasurementSnapshot().Select(measurement => measurement.Tags[AttachmentsMetrics.OutcomeTag]));
        Assert.All(downloads.GetMeasurementSnapshot(), measurement => Assert.Equal(1, measurement.Value));
    }

    [Fact]
    public void ReservationsSwept_Count_IsAddedWithoutTags()
    {
        using var swept = Collect("erp.attachments.reservations.swept");
        var metrics = new AttachmentsMetrics(MeterFactory);

        metrics.ReservationsSwept(3);

        var measurement = Assert.Single(swept.GetMeasurementSnapshot());
        Assert.Equal(3, measurement.Value);
        Assert.Empty(measurement.Tags);
    }

    [Fact]
    public void MeterName_TelemetryBaseline_ExportsIt()
    {
        Assert.Contains(TelemetryMeters.Names, name => name.EndsWith(".*", StringComparison.Ordinal)
            ? AttachmentsMetrics.MeterName.StartsWith(name[..^1], StringComparison.Ordinal)
            : string.Equals(name, AttachmentsMetrics.MeterName, StringComparison.Ordinal));
    }

    public void Dispose() => _services.Dispose();

    private MetricCollector<long> Collect(string instrument) => new(MeterFactory, AttachmentsMetrics.MeterName, instrument);
}
