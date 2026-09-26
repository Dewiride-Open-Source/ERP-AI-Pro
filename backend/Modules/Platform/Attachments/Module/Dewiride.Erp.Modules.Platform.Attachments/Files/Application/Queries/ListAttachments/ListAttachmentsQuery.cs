using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Queries.ListAttachments;

internal sealed record ListAttachmentsQuery(ListRequest Request) : IQuery<PagedResult<AttachmentDetails>>;
