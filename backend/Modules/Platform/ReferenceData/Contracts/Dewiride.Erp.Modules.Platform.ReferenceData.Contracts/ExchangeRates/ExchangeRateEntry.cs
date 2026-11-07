using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.ExchangeRates;

// The rate is the rupees for Unit units of the currency, as the source published it (FBIL quotes JPY per 100).
public sealed record ExchangeRateEntry(Currency Currency, DateOnly RateDate, decimal Rate, int Unit);
