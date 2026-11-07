namespace Dewiride.Erp.Modules.Platform.ReferenceData.Currencies.Endpoints.Responses;

/// <summary>One currency of ISO 4217.</summary>
/// <param name="Code">ISO 4217 alphabetic code of the currency, such as INR.</param>
/// <param name="Name">English name of the currency as ISO 4217 gives it.</param>
/// <param name="MinorUnits">Number of decimal places an amount in the currency has: 0, 2 or 3.</param>
internal sealed record CurrencyResponse(string Code, string Name, int MinorUnits);
