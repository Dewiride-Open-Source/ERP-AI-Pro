using System;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Time;

public static class IndianStandardTime
{
    public static readonly TimeSpan Offset = new(5, 30, 0);

    public static DateTimeOffset ToIst(DateTimeOffset instant) => instant.ToOffset(Offset);

    public static DateOnly IstDate(DateTimeOffset instant) => DateOnly.FromDateTime(ToIst(instant).DateTime);

    public static DateTimeOffset StartOfIstDay(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), Offset);

    public static DateOnly Today(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return IstDate(timeProvider.GetUtcNow());
    }
}
