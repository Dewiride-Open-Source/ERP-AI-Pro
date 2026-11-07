namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Endpoints.Responses;

/// <summary>The rupee value of a foreign currency on one day.</summary>
/// <param name="Id">Identifier of the exchange rate.</param>
/// <param name="Currency">ISO 4217 code of the foreign currency.</param>
/// <param name="RateDate">Day the rate is for.</param>
/// <param name="Rate">Rupees for Unit units of the currency, as the source published it.</param>
/// <param name="Unit">Units of the currency the rate is for: 1, 10, 100, 1,000, 10,000 or 100,000.</param>
/// <param name="Source">Where the rate comes from; null when not given.</param>
/// <param name="Version">Version of the rate, sent back with a correction.</param>
internal sealed record ExchangeRateResponse(Guid Id, string Currency, DateOnly RateDate, decimal Rate, int Unit, string? Source, string Version);
