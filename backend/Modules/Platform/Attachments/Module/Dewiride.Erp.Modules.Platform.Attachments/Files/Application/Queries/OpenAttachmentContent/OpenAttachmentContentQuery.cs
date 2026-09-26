using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Queries.OpenAttachmentContent;

internal sealed record OpenAttachmentContentQuery(AttachmentId Id, string? LinkToken) : IQuery<AttachmentDownload>;
