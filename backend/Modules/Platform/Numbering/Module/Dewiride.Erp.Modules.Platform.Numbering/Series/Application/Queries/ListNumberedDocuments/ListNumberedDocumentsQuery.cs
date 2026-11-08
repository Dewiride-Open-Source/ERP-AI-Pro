using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Numbering.Administration;

namespace Dewiride.Erp.Modules.Platform.Numbering.Series.Application.Queries.ListNumberedDocuments;

internal sealed record ListNumberedDocumentsQuery : IQuery<IReadOnlyList<NumberedDocumentDetails>>;
