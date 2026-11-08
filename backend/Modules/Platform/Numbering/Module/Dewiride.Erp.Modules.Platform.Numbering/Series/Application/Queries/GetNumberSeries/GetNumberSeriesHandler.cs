using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Numbering.Administration;

namespace Dewiride.Erp.Modules.Platform.Numbering.Series.Application.Queries.GetNumberSeries;

internal sealed class GetNumberSeriesHandler(INumberingAdministration administration) : IQueryHandler<GetNumberSeriesQuery, NumberedDocumentDetails>
{
    public Task<Result<NumberedDocumentDetails>> HandleAsync(GetNumberSeriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return administration.GetAsync(query.Id, cancellationToken);
    }
}
