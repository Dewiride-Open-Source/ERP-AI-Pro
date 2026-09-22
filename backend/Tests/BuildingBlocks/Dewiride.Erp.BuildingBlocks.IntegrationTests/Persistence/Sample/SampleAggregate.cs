using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;

public sealed class SampleAggregate : AggregateRoot<SampleId>, IAuditable, ISoftDeletable, IVersioned
{
    public const int NameMaxLength = 50;

    public const int Line1MaxLength = 100;

    public const int CityMaxLength = 50;

    private readonly List<SampleLine> _lines = [];

    private SampleAggregate(SampleId id)
        : base(id)
    {
    }

    public SampleAggregate(SampleId id, string name, Money price, SampleAddress address, DateTimeOffset occurredAt)
        : base(id)
    {
        Name = name;
        Price = price;
        Address = address;
        OccurredAt = occurredAt;
    }

    public string Name { get; private set; } = string.Empty;

    public Money Price { get; private set; }

    public SampleAddress Address { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public IReadOnlyList<SampleLine> Lines => _lines;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public void Rename(string name) => Name = name;

    public void Reprice(Money price) => Price = price;

    public SampleLine AddLine(string description)
    {
        var line = new SampleLine(SampleLineId.Create(), Id, description);
        _lines.Add(line);

        return line;
    }
}
