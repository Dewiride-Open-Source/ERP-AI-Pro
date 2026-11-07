using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Domain;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application;

internal sealed record ExchangeRateDetails(Guid Id, Currency Currency, DateOnly RateDate, decimal Rate, int Unit, string? Source, string Version)
{
    public static ExchangeRateDetails From(ExchangeRate entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new(entity.Id.Value, entity.Currency, entity.RateDate, entity.Rate, entity.Unit, entity.Source, RecordVersion.Encode(entity.RowVersion));
    }
}
