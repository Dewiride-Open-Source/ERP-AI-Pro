using System.Text;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Jobs.Queue;

public sealed class JobPayloadsTests
{
    private static readonly Guid PersonId = new("0198f6a2-7c1e-7b3a-9d4e-2f6a8b1c3d5e");

    [Fact]
    public void Serialize_Payload_UsesWebNamesAndEnumNamesAndReadsBackEqual()
    {
        var payload = new ReminderPayload(PersonId, new DateOnly(2026, 11, 2), ReminderKind.FinalNotice);

        var json = JobPayloads.Serialize(payload);

        Assert.Equal("""{"personId":"0198f6a2-7c1e-7b3a-9d4e-2f6a8b1c3d5e","due":"2026-11-02","kind":"finalNotice"}""", json);
        Assert.True(JobPayloads.TryDeserialize(json, typeof(ReminderPayload), out var read));
        Assert.Equal(payload, read);
    }

    [Fact]
    public void Serialize_PayloadOfTheLimit_IsAcceptedAndOneByteMoreIsRefused()
    {
        var frame = Encoding.UTF8.GetByteCount("""{"note":""}""");

        JobPayloads.Serialize(new NotePayload(new string('n', JobPayloads.MaxBytes - frame)));
        var refusal = Assert.Throws<ArgumentException>(() => JobPayloads.Serialize(new NotePayload(new string('n', JobPayloads.MaxBytes - frame + 1))));

        Assert.Contains(nameof(NotePayload), refusal.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("""{"personId":"0198f6a2-7c1e-7b3a-9d4e-2f6a8b1c3d5e","due":"2026-11-02","kind":2}""")]
    [InlineData("""{"personId":"not a guid","due":"2026-11-02","kind":"finalNotice"}""")]
    public void TryDeserialize_UnreadablePayload_IsRefused(string? json)
    {
        Assert.False(JobPayloads.TryDeserialize(json, typeof(ReminderPayload), out _));
    }

    [Fact]
    public void SameAs_StoredDocumentWrittenDifferently_MatchesThePayloadOfTheSameValues()
    {
        var json = JobPayloads.Serialize(new ReminderPayload(PersonId, new DateOnly(2026, 11, 2), ReminderKind.FirstNotice));
        const string Stored = """{ "kind": "firstNotice", "due": "2026-11-02", "personId": "0198F6A2-7C1E-7B3A-9D4E-2F6A8B1C3D5E" }""";

        Assert.True(JobPayloads.SameAs(Stored, json, typeof(ReminderPayload)));
        Assert.False(JobPayloads.SameAs(Stored, JobPayloads.Serialize(new ReminderPayload(PersonId, new DateOnly(2026, 11, 3), ReminderKind.FirstNotice)), typeof(ReminderPayload)));
    }

    [Fact]
    public void SameAs_JobWithoutPayload_MatchesOnlyAnotherWithout()
    {
        Assert.True(JobPayloads.SameAs(null, null, null));
        Assert.False(JobPayloads.SameAs("{}", null, null));
    }

    private enum ReminderKind
    {
        FirstNotice,
        FinalNotice,
    }

    private sealed record ReminderPayload(Guid PersonId, DateOnly Due, ReminderKind Kind);

    private sealed record NotePayload(string Note);
}
