using Dewiride.Erp.BuildingBlocks.Auditing.Security;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Auditing;

public sealed class SecurityEventTests
{
    private static readonly DateTimeOffset IndianMorning = new(2026, 10, 10, 9, 30, 0, TimeSpan.FromHours(5.5));

    [Fact]
    public void Record_EntryWithinItsColumns_KeepsEveryValueAtTheUtcTime()
    {
        var actor = Guid.CreateVersion7();
        var client = Guid.CreateVersion7();

        var recorded = SecurityEvent.Record(
            new SecurityEventEntry(SecurityEventKind.BearerTokenRefused, "expired", actor, client, "203.0.113.7", "0af7651916cd43dd8448eb211c80319c"),
            IndianMorning);

        Assert.NotEqual(Guid.Empty, recorded.Id.Value);
        Assert.Equal(TimeSpan.Zero, recorded.OccurredAt.Offset);
        Assert.Equal(IndianMorning, recorded.OccurredAt);
        Assert.Equal(SecurityEventKind.BearerTokenRefused, recorded.Kind);
        Assert.Equal("expired", recorded.Detail);
        Assert.Equal(actor, recorded.ActorObjectId);
        Assert.Equal(client, recorded.ClientApplicationId);
        Assert.Equal("203.0.113.7", recorded.ClientAddress);
        Assert.Equal("0af7651916cd43dd8448eb211c80319c", recorded.CorrelationId);
    }

    [Fact]
    public void Record_KindOutsideTheEnum_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SecurityEvent.Record(new SecurityEventEntry((SecurityEventKind)0), IndianMorning));
    }

    [Theory]
    [InlineData(nameof(SecurityEventEntry.Detail), SecurityEvent.DetailMaxLength)]
    [InlineData(nameof(SecurityEventEntry.ClientAddress), SecurityEvent.ClientAddressMaxLength)]
    [InlineData(nameof(SecurityEventEntry.CorrelationId), SecurityEvent.CorrelationIdMaxLength)]
    public void Record_ValueLongerThanItsColumn_ThrowsAndOneOfItsLengthPasses(string field, int maxLength)
    {
        SecurityEventEntry With(int length) => field switch
        {
            nameof(SecurityEventEntry.Detail) => new SecurityEventEntry(SecurityEventKind.SignInFailed, Detail: new string('d', length)),
            nameof(SecurityEventEntry.ClientAddress) => new SecurityEventEntry(SecurityEventKind.SignInFailed, ClientAddress: new string('a', length)),
            _ => new SecurityEventEntry(SecurityEventKind.SignInFailed, CorrelationId: new string('c', length)),
        };

        SecurityEvent.Record(With(maxLength), IndianMorning);
        var failure = Assert.Throws<ArgumentOutOfRangeException>(() => SecurityEvent.Record(With(maxLength + 1), IndianMorning));
        Assert.Equal(field, failure.ParamName);
    }
}
