using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Domain;

internal readonly record struct ExchangeRateId(Guid Value) : IStronglyTypedId<ExchangeRateId>
{
    public static ExchangeRateId Create() => new(Guid.CreateVersion7());

    public static ExchangeRateId From(Guid value) => new(value);
}
