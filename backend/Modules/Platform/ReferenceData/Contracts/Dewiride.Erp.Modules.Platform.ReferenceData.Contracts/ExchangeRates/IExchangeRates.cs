using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.ExchangeRates;

// Rates are published on business days only, so the rate of a date is the latest one recorded on or before it, and its
// own date says how old it is.
public interface IExchangeRates
{
    Task<ExchangeRateEntry?> RateOnAsync(Currency currency, DateOnly date, CancellationToken cancellationToken);
}
