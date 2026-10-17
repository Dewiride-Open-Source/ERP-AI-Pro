using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.Auditing.Changes;

public sealed class ChangeRecordTests
{
    private const string Key = "identity.users.people";

    private const string Changes = """[{"property":"name","kind":"text","after":"Asha"}]""";

    private static readonly DateTimeOffset IndianMorning = new(2026, 10, 16, 9, 30, 0, TimeSpan.FromHours(5.5));

    private static readonly Guid Entity = Guid.CreateVersion7();

    private static readonly Guid Subject = Guid.CreateVersion7();

    private static readonly Guid Actor = Guid.CreateVersion7();

    [Fact]
    public void Record_ValuesWithinTheirColumns_KeepsEveryValueAtTheUtcTime()
    {
        var record = ChangeRecord.Record(new ChangeStamp(IndianMorning, Actor, "203.0.113.7", "corr-1"), ChangeOperation.Created, Key, Entity, "identity.users.people", Subject, Changes, 1);

        Assert.Equal(7, record.Id.Value.Version);
        Assert.Equal(TimeSpan.Zero, record.OccurredAt.Offset);
        Assert.Equal(IndianMorning, record.OccurredAt);
        Assert.Equal(ChangeOperation.Created, record.Operation);
        Assert.Equal(Key, record.EntityType);
        Assert.Equal(Entity, record.EntityId);
        Assert.Equal(Key, record.SubjectType);
        Assert.Equal(Subject, record.SubjectId);
        Assert.Equal(Actor, record.ActorId);
        Assert.Equal("203.0.113.7", record.ClientAddress);
        Assert.Equal("corr-1", record.CorrelationId);
        Assert.Equal(Changes, record.Changes);
        Assert.Equal(1, record.ValueCount);
    }

    [Fact]
    public void Record_OperationOutsideTheEnum_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChangeRecord.Record(Stamp(), (ChangeOperation)0, Key, Entity, Key, Subject, null, 0));
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData(Changes, 0)]
    public void Record_ValueEntriesThatDisagreeWithTheirCount_Throws(string? changes, short valueCount)
    {
        Assert.Throws<ArgumentException>(() => ChangeRecord.Record(Stamp(), ChangeOperation.Updated, Key, Entity, Key, Subject, changes, valueCount));
    }

    [Fact]
    public void Record_NegativeCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChangeRecord.Record(Stamp(), ChangeOperation.Updated, Key, Entity, Key, Subject, Changes, -1));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Record_BlankKey_Throws(string key)
    {
        Assert.Throws<ArgumentException>(() => ChangeRecord.Record(Stamp(), ChangeOperation.Deleted, key, Entity, Key, Subject, null, 0));
        Assert.Throws<ArgumentException>(() => ChangeRecord.Record(Stamp(), ChangeOperation.Deleted, Key, Entity, key, Subject, null, 0));
    }

    [Theory]
    [InlineData("entityType", ChangeRecordMapping.KeyMaxLength)]
    [InlineData("subjectType", ChangeRecordMapping.KeyMaxLength)]
    [InlineData(nameof(ChangeStamp.ClientAddress), ChangeRecordMapping.ClientAddressMaxLength)]
    [InlineData(nameof(ChangeStamp.CorrelationId), ChangeRecordMapping.CorrelationIdMaxLength)]
    public void Record_ValueLongerThanItsColumn_ThrowsAndOneOfItsLengthPasses(string field, int maxLength)
    {
        ChangeRecord With(int length) => field switch
        {
            "entityType" => ChangeRecord.Record(Stamp(), ChangeOperation.Restored, new string('e', length), Entity, Key, Subject, null, 0),
            "subjectType" => ChangeRecord.Record(Stamp(), ChangeOperation.Restored, Key, Entity, new string('s', length), Subject, null, 0),
            nameof(ChangeStamp.ClientAddress) => ChangeRecord.Record(Stamp() with { ClientAddress = new string('a', length) }, ChangeOperation.Restored, Key, Entity, Key, Subject, null, 0),
            _ => ChangeRecord.Record(Stamp() with { CorrelationId = new string('c', length) }, ChangeOperation.Restored, Key, Entity, Key, Subject, null, 0),
        };

        With(maxLength);
        var failure = Assert.Throws<ArgumentOutOfRangeException>(() => With(maxLength + 1));
        Assert.Equal(field, failure.ParamName);
    }

    private static ChangeStamp Stamp() => new(IndianMorning, Actor, null, null);
}
