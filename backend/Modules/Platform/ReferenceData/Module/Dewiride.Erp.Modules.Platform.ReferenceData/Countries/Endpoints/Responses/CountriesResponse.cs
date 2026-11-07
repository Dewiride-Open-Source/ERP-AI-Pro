namespace Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Endpoints.Responses;

/// <summary>Every country the ERP knows, by name.</summary>
/// <param name="Countries">The countries, ordered by name.</param>
internal sealed record CountriesResponse(IReadOnlyList<CountryResponse> Countries);
