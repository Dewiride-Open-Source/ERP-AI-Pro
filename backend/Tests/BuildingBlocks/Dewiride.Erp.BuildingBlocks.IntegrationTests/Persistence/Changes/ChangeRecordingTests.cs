using System.Text.Json;
using System.Text.RegularExpressions;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Fakes;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample.Notes;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Changes;

public sealed partial class ChangeRecordingTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    private static readonly Guid Alice = new("33333333-3333-3333-3333-333333333333");

    private static readonly Guid Bob = new("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task SaveChangesAsync_AddedAggregate_RecordsItsCreationAtItsStampWithTheActorAndTheOrigin()
    {
        database.Actor.ActorId = Alice;
        var correlationId = Guid.CreateVersion7().ToString("N");
        database.Origin.ClientAddress = "198.51.100.23";
        database.Origin.CorrelationId = correlationId;
        var id = await AddSampleAsync("Created");

        var record = Assert.Single(await database.ChangeRecordsOfAsync(id.Value));

        var stored = await database.FindAsync(id);
        Assert.Equal(ChangeOperation.Created, record.Operation);
        Assert.Equal(SampleAggregateConfiguration.ChangeKey, record.EntityType);
        Assert.Equal(id.Value, record.EntityId);
        Assert.Equal(SampleAggregateConfiguration.ChangeKey, record.SubjectType);
        Assert.Equal(id.Value, record.SubjectId);
        Assert.Equal(stored.CreatedAt, record.OccurredAt);
        Assert.Equal(Alice, record.ActorId);
        Assert.Equal("198.51.100.23", record.ClientAddress);
        Assert.Equal(correlationId, record.CorrelationId);
        var values = Values(record);
        Assert.Equal(["address.city", "address.line1", "name", "occurredAt", "price.amount", "price.currency"], values.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(values.Count, record.ValueCount);
        Assert.Equal("Created", values["name"].GetProperty("after").GetString());
        Assert.Equal("10", values["price.amount"].GetProperty("after").GetString());
        Assert.Equal("number", values["price.amount"].GetProperty("kind").GetString());
        Assert.Equal("Jaipur", values["address.city"].GetProperty("after").GetString());
    }

    [Fact]
    public async Task SaveChangesAsync_RenamedAggregate_RecordsAnUpdateOfTheNameAtItsModifiedStamp()
    {
        database.Actor.ActorId = Alice;
        var id = await AddSampleAsync("Before");
        database.Clock.Advance(TimeSpan.FromMinutes(5));
        database.Actor.ActorId = Bob;

        await ChangeSampleAsync(id, sample => sample.Rename("After", database.Clock.GetUtcNow()));

        var records = await database.ChangeRecordsOfAsync(id.Value);
        Assert.Equal([ChangeOperation.Created, ChangeOperation.Updated], records.Select(record => record.Operation));
        var updated = records[1];
        Assert.Equal((await database.FindAsync(id)).ModifiedAt, updated.OccurredAt);
        Assert.Equal(Bob, updated.ActorId);
        var name = Assert.Single(Values(updated)).Value;
        Assert.Equal("Before", name.GetProperty("before").GetString());
        Assert.Equal("After", name.GetProperty("after").GetString());
    }

    [Fact]
    public async Task SaveChangesAsync_RepricedAggregate_RecordsTheAmountReadFromItsColumnAsTheAmountItIs()
    {
        var id = await AddSampleAsync("Priced");
        database.Clock.Advance(TimeSpan.FromMinutes(1));

        await ChangeSampleAsync(id, sample => sample.Reprice(new Money(12.5m, Currency.Inr)));

        var amount = Assert.Single(Values((await database.ChangeRecordsOfAsync(id.Value))[1])).Value;
        Assert.Equal("price.amount", amount.GetProperty("property").GetString());
        Assert.Equal("10", amount.GetProperty("before").GetString());
        Assert.Equal("12.5", amount.GetProperty("after").GetString());
    }

    [Fact]
    public async Task SaveChangesAsync_SoftDeletedThenRestoredAggregate_RecordsTheDeletionAndTheRestorationAtTheirStamps()
    {
        var id = await AddSampleAsync("Deleted then restored");
        database.Clock.Advance(TimeSpan.FromMinutes(1));
        await using (var scope = database.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            context.Samples.Remove(await context.Samples.SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var deletedAt = (await database.FindAsync(id, includeDeleted: true)).DeletedAt;
        database.Clock.Advance(TimeSpan.FromMinutes(1));
        await using (var scope = database.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            var sample = await context.Samples.IncludeDeleted().SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
            context.Entry(sample).Property(s => s.IsDeleted).CurrentValue = false;
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var records = await database.ChangeRecordsOfAsync(id.Value);
        Assert.Equal([ChangeOperation.Created, ChangeOperation.Deleted, ChangeOperation.Restored], records.Select(record => record.Operation));
        Assert.Equal(deletedAt, records[1].OccurredAt);
        Assert.Equal((await database.FindAsync(id)).ModifiedAt, records[2].OccurredAt);
        Assert.All(records.Skip(1), record => Assert.Null(record.Changes));
    }

    [Fact]
    public async Task SaveChangesAsync_NoteAddedChangedAndRemoved_RecordsEachUnderItsSampleAndTheRemovalWithItsLastValues()
    {
        var sampleId = await AddSampleAsync("Noted");
        var noteId = SampleNoteId.Create();
        await using (var scope = database.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            context.Notes.Add(new SampleNote(noteId, sampleId, "Call the auditor", new DateOnly(2027, 3, 31), ["finance"]));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        database.Clock.Advance(TimeSpan.FromMinutes(1));
        await ChangeNoteAsync(noteId, note => note.Retag(["finance", "year-end"]));
        database.Clock.Advance(TimeSpan.FromMinutes(1));
        await using (var scope = database.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            context.Notes.Remove(await context.Notes.SingleAsync(n => n.Id == noteId, TestContext.Current.CancellationToken));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var records = await database.ChangeRecordsOfAsync(noteId.Value);
        Assert.Equal([ChangeOperation.Created, ChangeOperation.Updated, ChangeOperation.Deleted], records.Select(record => record.Operation));
        Assert.All(records, record =>
        {
            Assert.Equal(SampleNoteConfiguration.ChangeKey, record.EntityType);
            Assert.Equal(SampleAggregateConfiguration.ChangeKey, record.SubjectType);
            Assert.Equal(sampleId.Value, record.SubjectId);
        });
        var created = Values(records[0]);
        Assert.Equal("reference", created["sampleId"].GetProperty("kind").GetString());
        Assert.Equal(SampleAggregateConfiguration.ChangeKey, created["sampleId"].GetProperty("references").GetString());
        Assert.Equal("2027-03-31", created["dueOn"].GetProperty("after").GetString());
        var tags = Assert.Single(Values(records[1])).Value;
        Assert.Equal(["finance"], tags.GetProperty("before").EnumerateArray().Select(tag => tag.GetString()));
        Assert.Equal(["finance", "year-end"], tags.GetProperty("after").EnumerateArray().Select(tag => tag.GetString()));
        var removed = Values(records[2]);
        Assert.Equal(["dueOn", "sampleId", "tags", "text"], removed.Keys.Order(StringComparer.Ordinal));
        Assert.All(removed.Values, value => Assert.False(value.TryGetProperty("after", out _)));
        Assert.Equal("Call the auditor", removed["text"].GetProperty("before").GetString());
        Assert.Equal(["finance", "year-end"], removed["tags"].GetProperty("before").EnumerateArray().Select(tag => tag.GetString()));
    }

    [Fact]
    public async Task SaveChangesAsync_ChildThatIsNotRecorded_AddsNoRecord()
    {
        var id = await AddSampleAsync("Parent");
        database.Clock.Advance(TimeSpan.FromMinutes(1));

        await ChangeSampleAsync(id, sample => sample.AddLine("a line of its own"));

        Assert.Single(await database.ChangeRecordsOfAsync(id.Value));
    }

    [Fact]
    public async Task SaveChangesAsync_SeveralRecordsInOneSave_AreInsertedByOnePlainMultiRowInsert()
    {
        var log = new CommandLog();
        var sampleId = SampleId.Create();
        await using (var context = database.CreateContext(log))
        {
            context.Samples.Add(new SampleAggregate(sampleId, "Batched", new Money(1m, Currency.Inr), new SampleAddress("1", "Pune"), database.Clock.GetUtcNow()));
            context.Notes.Add(new SampleNote(SampleNoteId.Create(), sampleId, "First", null, []));
            context.Notes.Add(new SampleNote(SampleNoteId.Create(), sampleId, "Second", null, []));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var insert = Assert.Single(log.Commands, command => command.Contains("[audit].[ChangeRecords]", StringComparison.Ordinal));
        Assert.DoesNotContain("MERGE", insert, StringComparison.OrdinalIgnoreCase);
        var statement = ChangeRecordsInsert().Match(insert);
        Assert.True(statement.Success, insert);
        Assert.Equal(3, statement.Groups["row"].Captures.Count);
    }

    private async Task<SampleId> AddSampleAsync(string name)
    {
        var id = SampleId.Create();
        await database.AddAsync(new SampleAggregate(id, name, new Money(10m, Currency.Inr), new SampleAddress("1", "Jaipur"), database.Clock.GetUtcNow()));

        return id;
    }

    private async Task ChangeSampleAsync(SampleId id, Action<SampleAggregate> change)
    {
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var sample = await context.Samples.SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
        change(sample);
        sample.ClearDomainEvents();
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task ChangeNoteAsync(SampleNoteId id, Action<SampleNote> change)
    {
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        change(await context.Notes.SingleAsync(n => n.Id == id, TestContext.Current.CancellationToken));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Dictionary<string, JsonElement> Values(ChangeRecord record)
    {
        using var document = JsonDocument.Parse(record.Changes ?? "[]");

        return document.RootElement.EnumerateArray().ToDictionary(value => value.GetProperty("property").GetString()!, value => value.Clone(), StringComparer.Ordinal);
    }

    [GeneratedRegex(@"INSERT INTO \[audit\]\.\[ChangeRecords\] \([^)]*\)\s*VALUES\s*(?<row>\([^)]*\)(,\s*)?)+;")]
    private static partial Regex ChangeRecordsInsert();
}
