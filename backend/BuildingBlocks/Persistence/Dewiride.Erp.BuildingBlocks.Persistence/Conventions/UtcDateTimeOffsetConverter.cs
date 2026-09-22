using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Conventions;

public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    public UtcDateTimeOffsetConverter()
        : base(value => value.ToUniversalTime(), value => value.ToUniversalTime())
    {
    }
}
