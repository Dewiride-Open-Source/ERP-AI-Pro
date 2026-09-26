using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Queries.GetAttachment;

internal sealed record GetAttachmentQuery(AttachmentId Id) : IQuery<AttachmentDetails>;
