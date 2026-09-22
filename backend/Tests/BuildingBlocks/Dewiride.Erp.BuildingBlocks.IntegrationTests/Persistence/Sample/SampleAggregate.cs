using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;

public sealed class SampleAggregate : AggregateRoot<SampleId>
{
    public const int NameMaxLength = 50;

    public const int Line1MaxLength = 100;

    public const int CityMaxLength = 50;

    private SampleAggregate(SampleId id)
        : base(id)
    {
    }

    public SampleAggregate(SampleId id, string name, decimal price, SampleAddress address, DateTimeOffset occurredAt)
        : base(id)
    {
        Name = name;
        Price = price;
        Address = address;
        OccurredAt = occurredAt;
    }

    public string Name { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public SampleAddress Address { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
