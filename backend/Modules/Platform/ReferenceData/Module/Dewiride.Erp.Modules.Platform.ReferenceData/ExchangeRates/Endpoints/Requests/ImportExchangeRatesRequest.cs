using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Endpoints.Requests;

public sealed record ImportExchangeRatesRequest(
    [property: Description("The rates of the file, in its order, at most 1,000; recorded all together or not at all.")]
    [property: Required]
    [property: Length(1, ExchangeRateLimits.MaxImportedRates)]
    IReadOnlyList<ImportedExchangeRateRequest>? Rates);
