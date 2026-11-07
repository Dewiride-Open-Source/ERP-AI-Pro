using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.ExchangeRates;
using Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.PublicApi;

internal sealed class ExchangeRateLookup(ReferenceDataDbContext context) : IExchangeRates
{
    public async Task<ExchangeRateEntry?> RateOnAsync(Currency currency, DateOnly date, CancellationToken cancellationToken) =>
        await context.ExchangeRates
            .AsNoTracking()
            .Where(e => e.Currency == currency && e.RateDate <= date)
            .OrderByDescending(e => e.RateDate)
            .Select(e => new ExchangeRateEntry(e.Currency, e.RateDate, e.Rate, e.Unit))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
}
