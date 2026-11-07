using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Queries.GetExchangeRate;

internal sealed class GetExchangeRateHandler(ReferenceDataDbContext context) : IQueryHandler<GetExchangeRateQuery, ExchangeRateDetails>
{
    public async Task<Result<ExchangeRateDetails>> HandleAsync(GetExchangeRateQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var id = ExchangeRateId.From(query.Id);
        var found = await context.ExchangeRates.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);

        return found is null ? ExchangeRateErrors.NotFound : ExchangeRateDetails.From(found);
    }
}
