using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Queries.GetUploadPolicy;

internal sealed class GetUploadPolicyHandler(IAttachmentService attachments) : IQueryHandler<GetUploadPolicyQuery, UploadPolicy>
{
    public Task<Result<UploadPolicy>> HandleAsync(GetUploadPolicyQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<Result<UploadPolicy>>(attachments.GetUploadPolicy());
}
