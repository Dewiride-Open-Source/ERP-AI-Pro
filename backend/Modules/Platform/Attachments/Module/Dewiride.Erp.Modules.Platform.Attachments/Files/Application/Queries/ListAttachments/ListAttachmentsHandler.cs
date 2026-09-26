using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Queries.ListAttachments;

internal sealed class ListAttachmentsHandler(IAttachmentService attachments) : IQueryHandler<ListAttachmentsQuery, PagedResult<AttachmentDetails>>
{
    public Task<Result<PagedResult<AttachmentDetails>>> HandleAsync(ListAttachmentsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return attachments.ListAsync(query.Request, cancellationToken);
    }
}
