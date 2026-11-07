using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Domain;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application;

internal static class ExchangeRateLimits
{
    public const string CurrencyPattern = "^[A-Z]{3}$";

    public const int CurrencyLength = 3;

    public const int SourceMaxLength = ExchangeRate.SourceMaxLength;

    public const int VersionMaxLength = RecordVersion.InputMaxLength;

    public const int MaxImportedRates = 1_000;
}
