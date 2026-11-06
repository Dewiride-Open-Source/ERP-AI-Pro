using System.Text;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Publishing;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.IntegrationEvents.Events;

public sealed class IntegrationEventSerializerTests
{
    private const string Readable = """{"subjectId":"0198f6a2-7c1e-7b3a-9d4e-2f6a8b1c3d5e","smallest":-2147483648,"largest":2147483647,"at":"2026-11-06T18:31:00+05:30","on":"2026-11-07","kind":"joiner","members":["0198f6a2-7c1e-7b3a-9d4e-2f6a8b1c3d5f"]}""";

    [Fact]
    public void TryDeserialize_EventItSerialized_ReadsBackEqualWithTheBoundsOfAnInt()
    {
        var sent = new ProbeEvent(Guid.CreateVersion7(), int.MinValue, int.MaxValue, new DateTimeOffset(2026, 11, 6, 18, 31, 0, TimeSpan.FromHours(5.5)), new DateOnly(2026, 11, 7), ProbeKind.Leaver, [Guid.CreateVersion7()], Guid.CreateVersion7());
        using var json = JsonDocument.Parse(IntegrationEventSerializer.Serialize(sent));

        Assert.True(IntegrationEventSerializer.TryDeserialize<ProbeEvent>(json.RootElement, out var read));
        Assert.Equal((sent.SubjectId, sent.Smallest, sent.Largest, sent.At, sent.On, sent.Kind, sent.ManagerId), (read.SubjectId, read.Smallest, read.Largest, read.At, read.On, read.Kind, read.ManagerId));
        Assert.Equal(sent.Members, read.Members);
    }

    [Fact]
    public void TryDeserialize_OptionalMemberAbsentAndUnknownMemberPresent_ReadsTheEvent()
    {
        using var json = JsonDocument.Parse(Readable.Replace("}", ""","colour":"red"}""", StringComparison.Ordinal));

        Assert.True(IntegrationEventSerializer.TryDeserialize<ProbeEvent>(json.RootElement, out var read));
        Assert.Null(read.ManagerId);
    }

    [Theory]
    [InlineData("\"largest\":2147483647,", "")]
    [InlineData("[\"0198f6a2-7c1e-7b3a-9d4e-2f6a8b1c3d5f\"]", "null")]
    [InlineData("2147483647", "\"2147483647\"")]
    [InlineData("\"joiner\"", "0")]
    public void TryDeserialize_MissingMemberNullWhereNoneIsAllowedOrLooseValue_IsUnreadable(string part, string replacement)
    {
        using var json = JsonDocument.Parse(Readable.Replace(part, replacement, StringComparison.Ordinal));

        Assert.False(IntegrationEventSerializer.TryDeserialize<ProbeEvent>(json.RootElement, out _));
    }

    [Fact]
    public void Serialize_EventOverTheLimit_IsRefusedNamingTheEvent()
    {
        var members = Enumerable.Range(0, IntegrationEventSerializer.MaxBytes / 36).Select(_ => Guid.CreateVersion7()).ToList();
        var oversized = new ProbeEvent(Guid.CreateVersion7(), 1, 2, DateTimeOffset.UnixEpoch, DateOnly.MinValue, ProbeKind.Joiner, members);

        var refusal = Assert.Throws<ArgumentException>(() => IntegrationEventSerializer.Serialize(oversized));

        Assert.True(Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(oversized)) > IntegrationEventSerializer.MaxBytes);
        Assert.Contains(nameof(ProbeEvent), refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_EventWhoseTimesGrowWhenADeliveryWritesThem_IsMeasuredAsTheDeliveryCarriesIt()
    {
        var timetable = TimetableOf(500);

        Assert.True(JsonSerializer.SerializeToUtf8Bytes(timetable, IntegrationEventSerializer.Options).Length <= IntegrationEventSerializer.MaxBytes);
        Assert.Throws<ArgumentException>(() => IntegrationEventSerializer.Serialize(timetable));
    }

    [Fact]
    public void Serialize_LargestEvent_FitsAJobPayloadInsideTheLargestDelivery()
    {
        var count = 0;
        while (Fits(TimetableOf(count + 1)))
        {
            count++;
        }

        using var largest = JsonDocument.Parse(IntegrationEventSerializer.Serialize(TimetableOf(count)));
        var delivery = new IntegrationEventDelivery(
            Guid.CreateVersion7(),
            new string('e', IntegrationEventNames.MaxLength),
            new string('s', 128),
            new DateTimeOffset(2026, 11, 6, 18, 31, 0, TimeSpan.FromHours(5.5)),
            new string('+', OutboxMessage.CorrelationIdMaxLength),
            IntegrationEventChain.MaxDepth,
            largest.RootElement.Clone());

        Assert.True(Encoding.UTF8.GetByteCount(JobPayloads.Serialize(delivery)) <= JobPayloads.MaxBytes);
        Assert.False(Fits(TimetableOf(count + 1)));
    }

    private static ProbeTimetable TimetableOf(int count) =>
        new(Guid.CreateVersion7(), [.. Enumerable.Range(0, count).Select(index => new DateTimeOffset(2026, 11, 6, 4, 30, 0, TimeSpan.Zero).AddDays(index))]);

    private static bool Fits(ProbeTimetable timetable)
    {
        try
        {
            IntegrationEventSerializer.Serialize(timetable);
        }
        catch (ArgumentException)
        {
            return false;
        }

        return true;
    }
}
