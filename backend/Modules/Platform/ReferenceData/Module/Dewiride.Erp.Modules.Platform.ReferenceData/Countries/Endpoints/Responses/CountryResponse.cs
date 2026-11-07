namespace Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Endpoints.Responses;

/// <summary>One country.</summary>
/// <param name="Code">ISO 3166-1 alpha-2 code of the country, such as IN; XK for Kosovo, which the GST e-invoice system accepts.</param>
/// <param name="Name">English short name of the country.</param>
internal sealed record CountryResponse(string Code, string Name);
