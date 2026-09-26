using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Queries.OpenAttachmentContent;

internal sealed class OpenAttachmentContentHandler(IAttachmentService attachments) : IQueryHandler<OpenAttachmentContentQuery, AttachmentDownload>
{
    public Task<Result<AttachmentDownload>> HandleAsync(OpenAttachmentContentQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return attachments.OpenDownloadAsync(query.Id, query.LinkToken, cancellationToken);
    }
}
