namespace Dewiride.Erp.Modules.Platform.ReferenceData.Currencies.Endpoints.Responses;

/// <summary>Every currency an amount in the ERP can be in.</summary>
/// <param name="Currencies">The currencies, ordered by code.</param>
internal sealed record CurrenciesResponse(IReadOnlyList<CurrencyResponse> Currencies);
