using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Attachments.UnitTests.Files.Application;

internal sealed class RecordingAttachmentService : IAttachmentService
{
    public static AttachmentDetails SampleDetails { get; } = new(
        AttachmentId.Create(),
        "statement.pdf",
        "application/pdf",
        2048,
        new string('a', 64),
        AttachmentScanStatus.NotScanned,
        new DateTimeOffset(2026, 9, 26, 6, 30, 0, TimeSpan.Zero),
        ActorIds.Anonymous);

    public UploadPolicy Policy { get; init; } = new(1024, ["application/pdf"]);

    public Result<AttachmentDetails> Details { get; init; } = SampleDetails;

    public Result<PagedResult<AttachmentDetails>> Page { get; init; } = new PagedResult<AttachmentDetails>([SampleDetails], 1, 50, 1);

    public Result Deletion { get; init; } = Result.Success();

    public Result<DownloadLinkDetails> Link { get; init; } = new DownloadLinkDetails("token", new DateTimeOffset(2026, 9, 26, 6, 35, 0, TimeSpan.Zero));

    public Result<AttachmentDownload> Download { get; init; } = AttachmentErrors.NotFound;

    public AttachmentUpload? ReceivedUpload { get; private set; }

    public AttachmentId? ReceivedId { get; private set; }

    public ListRequest? ReceivedRequest { get; private set; }

    public string? ReceivedToken { get; private set; }

    public CancellationToken ReceivedCancellation { get; private set; }

    public UploadPolicy GetUploadPolicy() => Policy;

    public Task<Result<AttachmentDetails>> UploadAsync(AttachmentUpload upload, CancellationToken cancellationToken)
    {
        ReceivedUpload = upload;
        ReceivedCancellation = cancellationToken;

        return Task.FromResult(Details);
    }

    public Task<Result<AttachmentDetails>> GetAsync(AttachmentId id, CancellationToken cancellationToken)
    {
        ReceivedId = id;
        ReceivedCancellation = cancellationToken;

        return Task.FromResult(Details);
    }

    public Task<Result<PagedResult<AttachmentDetails>>> ListAsync(ListRequest request, CancellationToken cancellationToken)
    {
        ReceivedRequest = request;
        ReceivedCancellation = cancellationToken;

        return Task.FromResult(Page);
    }

    public Task<Result> DeleteAsync(AttachmentId id, CancellationToken cancellationToken)
    {
        ReceivedId = id;
        ReceivedCancellation = cancellationToken;

        return Task.FromResult(Deletion);
    }

    public Task<Result<DownloadLinkDetails>> CreateDownloadLinkAsync(AttachmentId id, CancellationToken cancellationToken)
    {
        ReceivedId = id;
        ReceivedCancellation = cancellationToken;

        return Task.FromResult(Link);
    }

    public Task<Result<AttachmentDownload>> OpenDownloadAsync(AttachmentId id, string? token, CancellationToken cancellationToken)
    {
        ReceivedId = id;
        ReceivedToken = token;
        ReceivedCancellation = cancellationToken;

        return Task.FromResult(Download);
    }
}
