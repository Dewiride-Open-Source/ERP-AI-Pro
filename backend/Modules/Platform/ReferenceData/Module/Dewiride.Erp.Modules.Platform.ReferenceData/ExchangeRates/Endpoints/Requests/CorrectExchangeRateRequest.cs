using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Endpoints.Requests;

public sealed record CorrectExchangeRateRequest(
    [property: Description("Rupees for the unit of the currency, more than 0 and at most 1,000,000, with at most four decimal places.")]
    [property: Required]
    decimal? Rate,
    [property: Description("Units of the currency the rate is for: 1, 10, 100, 1,000, 10,000 or 100,000, as the source quotes it.")]
    [property: Required]
    int? Unit,
    [property: Description("Where the rate comes from, such as FBIL reference rate; absent or blank when not given.")]
    [property: StringLength(ExchangeRateLimits.SourceMaxLength)]
    string? Source,
    [property: Description("Version of the rate the correction was made from, as the API last returned it.")]
    [property: Required]
    [property: StringLength(ExchangeRateLimits.VersionMaxLength)]
    string? Version);
