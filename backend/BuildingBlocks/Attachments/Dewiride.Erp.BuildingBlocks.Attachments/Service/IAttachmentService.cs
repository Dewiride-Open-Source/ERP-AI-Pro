using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Service;

public interface IAttachmentService
{
    UploadPolicy GetUploadPolicy();

    Task<Result<AttachmentDetails>> UploadAsync(AttachmentUpload upload, CancellationToken cancellationToken);

    Task<Result<AttachmentDetails>> GetAsync(AttachmentId id, CancellationToken cancellationToken);

    Task<Result<PagedResult<AttachmentDetails>>> ListAsync(ListRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(AttachmentId id, CancellationToken cancellationToken);

    Task<Result<DownloadLinkDetails>> CreateDownloadLinkAsync(AttachmentId id, CancellationToken cancellationToken);

    Task<Result<AttachmentDownload>> OpenDownloadAsync(AttachmentId id, string? token, CancellationToken cancellationToken);
}
