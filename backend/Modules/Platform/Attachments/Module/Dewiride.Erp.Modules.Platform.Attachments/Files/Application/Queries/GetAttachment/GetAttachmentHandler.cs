using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Queries.GetAttachment;

internal sealed class GetAttachmentHandler(IAttachmentService attachments) : IQueryHandler<GetAttachmentQuery, AttachmentDetails>
{
    public Task<Result<AttachmentDetails>> HandleAsync(GetAttachmentQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return attachments.GetAsync(query.Id, cancellationToken);
    }
}
