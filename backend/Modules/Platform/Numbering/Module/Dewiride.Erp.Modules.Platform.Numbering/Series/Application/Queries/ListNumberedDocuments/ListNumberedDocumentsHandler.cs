using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Numbering.Administration;

namespace Dewiride.Erp.Modules.Platform.Numbering.Series.Application.Queries.ListNumberedDocuments;

internal sealed class ListNumberedDocumentsHandler(INumberingAdministration administration) : IQueryHandler<ListNumberedDocumentsQuery, IReadOnlyList<NumberedDocumentDetails>>
{
    public async Task<Result<IReadOnlyList<NumberedDocumentDetails>>> HandleAsync(ListNumberedDocumentsQuery query, CancellationToken cancellationToken) =>
        Result.Success(await administration.ListAsync(cancellationToken).ConfigureAwait(false));
}
