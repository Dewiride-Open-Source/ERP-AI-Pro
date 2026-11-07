namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Endpoints.Responses;

/// <summary>What an import recorded.</summary>
/// <param name="Recorded">Rates of the file that were recorded.</param>
/// <param name="Unchanged">Rates of the file that were already recorded with the same rate and unit, and were left as they were.</param>
internal sealed record ImportedExchangeRatesResponse(int Recorded, int Unchanged);
