using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Origins;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using static Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.Auditing.Changes.CaptureContext;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.Auditing.Changes;

public sealed class ChangeCaptureTests
{
    private const string ClientAddress = "203.0.113.7";

    private const string CorrelationId = "4bf92f3577b34da6a3ce929d0e0e4736";

    private static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Earlier = new(2026, 4, 1, 4, 0, 0, TimeSpan.Zero);

    private static readonly Guid Actor = new("0199a1b2-0000-7000-8000-0000000000aa");

    private static readonly Guid Creator = new("0199a1b2-0000-7000-8000-0000000000bb");

    [Fact]
    public async Task SaveChangesAsync_AddedEntity_RecordsItsCreationWithEveryRecordedValueAfter()
    {
        var capture = new CapturingSaveChangesInterceptor();
        using var context = Context(capture);
        var category = CategoryId.Create();
        var external = Guid.CreateVersion7();
        var widget = new Widget
        {
            Name = "Gear",
            Quantity = 3,
            Price = new Money(12.50m, Currency.Inr),
            Size = new Size(2.5m, 4m),
            Colour = Colour.DeepBlue,
            Finishes = [Colour.Red, Colour.DeepBlue],
            ReleasedOn = new DateOnly(2026, 4, 1),
            InspectedAt = new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.FromHours(5.5)),
            IsFeatured = true,
            ExternalId = external,
            Thumbprint = [1, 2, 3],
            Secret = "hidden",
            CategoryId = category,
        };
        context.Add(widget);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var record = Assert.Single(capture.Records);
        Assert.Equal(ChangeOperation.Created, record.Operation);
        Assert.Equal(WidgetKey, record.EntityType);
        Assert.Equal(widget.Id.Value, record.EntityId);
        Assert.Equal(WidgetKey, record.SubjectType);
        Assert.Equal(widget.Id.Value, record.SubjectId);
        var values = Values(record);
        Assert.Equal(
            ["categoryId", "colour", "externalId", "finishes", "inspectedAt", "isFeatured", "name", "price.amount", "price.currency", "quantity", "releasedOn", "size.height", "size.width", "thumbprint"],
            values.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(values.Count, record.ValueCount);
        Assert.All(values.Values, value => Assert.False(value.TryGetProperty("before", out _)));
        AssertAfter(values["name"], "text", "Gear");
        AssertAfter(values["quantity"], "number", "3");
        AssertAfter(values["price.amount"], "number", "12.5");
        AssertAfter(values["price.currency"], "text", "INR");
        AssertAfter(values["size.width"], "number", "2.5");
        AssertAfter(values["size.height"], "number", "4");
        AssertAfter(values["colour"], "enum", "deepBlue");
        AssertAfter(values["releasedOn"], "date", "2026-04-01");
        AssertAfter(values["inspectedAt"], "instant", "2026-04-01T04:30:00.0000000+00:00");
        AssertAfter(values["externalId"], "id", external.ToString("D"));
        AssertAfter(values["thumbprint"], "binary", "AQID");
        AssertAfter(values["categoryId"], "reference", category.Value.ToString("D"));
        Assert.Equal(CategoryKey, values["categoryId"].GetProperty("references").GetString());
        Assert.Equal("boolean", values["isFeatured"].GetProperty("kind").GetString());
        Assert.True(values["isFeatured"].GetProperty("after").GetBoolean());
        Assert.Equal("list", values["finishes"].GetProperty("kind").GetString());
        Assert.Equal(["red", "deepBlue"], values["finishes"].GetProperty("after").EnumerateArray().Select(element => element.GetString()));
    }

    [Fact]
    public async Task SaveChangesAsync_AnySave_StampsTheRecordWithTheTimeActorAndOriginOfItsRow()
    {
        var capture = new CapturingSaveChangesInterceptor();
        using var context = Context(capture);
        var widget = new Widget { Name = "Stamped" };
        context.Add(widget);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var record = Assert.Single(capture.Records);
        Assert.Equal(widget.CreatedAt, record.OccurredAt);
        Assert.Equal(Now, record.OccurredAt);
        Assert.Equal(Actor, record.ActorId);
        Assert.Equal(ClientAddress, record.ClientAddress);
        Assert.Equal(CorrelationId, record.CorrelationId);
        Assert.Equal(7, record.Id.Value.Version);
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedEntity_RecordsAnUpdateOfOnlyTheValuesThatChanged()
    {
        var capture = new CapturingSaveChangesInterceptor();
        using var context = Context(capture);
        var widget = Persisted(context, new Widget { Name = "Before", Quantity = 1, ReleasedOn = new DateOnly(2026, 4, 1) });
        widget.Name = "After";
        widget.ReleasedOn = null;

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var record = Assert.Single(capture.Records);
        Assert.Equal(ChangeOperation.Updated, record.Operation);
        Assert.Equal(widget.ModifiedAt, record.OccurredAt);
        var values = Values(record);
        Assert.Equal(["name", "releasedOn"], values.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("Before", values["name"].GetProperty("before").GetString());
        Assert.Equal("After", values["name"].GetProperty("after").GetString());
        Assert.Equal("2026-04-01", values["releasedOn"].GetProperty("before").GetString());
        Assert.Equal(JsonValueKind.Null, values["releasedOn"].GetProperty("after").ValueKind);
    }

    [Fact]
    public async Task SaveChangesAsync_UpdateOfOnlyAnExcludedValue_RecordsNothing()
    {
        var capture = new CapturingSaveChangesInterceptor();
        using var context = Context(capture);
        var widget = Persisted(context, new Widget { Name = "Quiet", Secret = "first" });
        widget.Secret = "second";

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(EntityState.Modified, context.Entry(widget).State);
        Assert.Empty(capture.Records);
    }

    [Fact]
    public async Task SaveChangesAsync_AmountReadWithTrailingZerosSetToTheSameAmount_RecordsNothing()
    {
        var capture = new CapturingSaveChangesInterceptor();
        using var context = Context(capture);
        var widget = Persisted(context, new Widget { Price = new Money(10.0000m, Currency.Inr) });
        widget.Price = new Money(10m, Currency.Inr);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(capture.Records);
    }

    [Fact]
    public async Task SaveChangesAsync_ComplexMemberChanged_RecordsItByItsDottedPath()
    {
        var capture = new CapturingSaveChangesInterceptor();
        using var context = Context(capture);
        var widget = Persisted(context, new Widget { Price = new Money(10.0000m, Currency.Inr), Size = new Size(1m, 2m) });
        widget.Price = new Money(12.5m, Currency.Inr);
        widget.Size = widget.Size with { Height = 3m };

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var values = Values(Assert.Single(capture.Records));
        Assert.Equal(["price.amount", "size.height"], values.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("10", values["price.amount"].GetProperty("before").GetString());
        Assert.Equal("12.5", values["price.amount"].GetProperty("after").GetString());
        Assert.Equal("2", values["size.height"].GetProperty("before").GetString());
        Assert.Equal("3", values["size.height"].GetProperty("after").GetString());
    }

    [Fact]
    public async Task SaveChangesAsync_PrimitiveCollectionChanged_RecordsBothLists()
    {
        var capture = new CapturingSaveChangesInterceptor();
        using var context = Context(capture);
        var widget = Persisted(context, new Widget { Finishes = [Colour.Red] });
        widget.Finishes = [Colour.Red, Colour.DeepBlue];

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var finishes = Assert.Single(Values(Assert.Single(capture.Records))).Value;
        Assert.Equal(["red"], finishes.GetProperty("before").EnumerateArray().Select(element => element.GetString()));
        Assert.Equal(["red", "deepBlue"], finishes.GetProperty("after").EnumerateArray().Select(element => element.GetString()));
    }

    [Fact]
    public async Task SaveChangesAsync_RemovedSoftDeletableEntity_RecordsItsDeletionAtItsDeletionStamp()
    {
        var capture = new CapturingSaveChangesInterceptor();
        using var context = Context(capture);
        var widget = Persisted(context, new Widget { Name = "Gone" });
        context.Remove(widget);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var record = Assert.Single(capture.Records);
        Assert.Equal(ChangeOperation.Deleted, record.Operation);
        Assert.Equal(widget.DeletedAt, record.OccurredAt);
        Assert.Null(record.Changes);
        Assert.Equal(0, record.ValueCount);
    }

    [Fact]
    public async Task SaveChangesAsync_SoftDeletionWithOtherChanges_RecordsTheDeletionWithThoseValues()
    {
        var capture = new CapturingSaveChangesInterceptor();
        using var context = Context(capture);
        var widget = Persisted(context, new Widget { Name = "Closing" });
        widget.Name = "Closed";
        widget.IsDeleted = true;

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var record = Assert.Single(capture.Records);
        Assert.Equal(ChangeOperation.Deleted, record.Operation);
        Assert.Equal(["name"], Values(record).Keys);
    }

    [Fact]
    public async Task SaveChangesAsync_RestoredEntity_RecordsItsRestoration()
    {
        var capture = new CapturingSaveChangesInterceptor();
        using var context = Context(capture);
        var widget = Persisted(context, new Widget { Name = "Back", IsDeleted = true, DeletedAt = Earlier, DeletedBy = Creator });
        widget.IsDeleted = false;

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var record = Assert.Single(capture.Records);
        Assert.Equal(ChangeOperation.Restored, record.Operation);
        Assert.Null(record.Changes);
    }

    [Fact]
    public async Task SaveChangesAsync_DeletedEntityRemovedAgain_RecordsNothing()
    {
        var capture = new CapturingSaveChangesInterceptor();
        using var context = Context(capture);
        var widget = Persisted(context, new Widget { IsDeleted = true, DeletedAt = Earlier, DeletedBy = Creator });
        context.Remove(widget);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(capture.Records);
    }

    [Fact]
    public async Task SaveChangesAsync_HardDeletedEntity_RecordsItsDeletionWithEveryOriginalValueBeforeUnderItsSubject()
    {
        var capture = new CapturingSaveChangesInterceptor();
        using var context = Context(capture);
        var widget = WidgetId.Create();
        var part = Persisted(context, new Part { WidgetId = widget, Label = "Spring" });
        part.Label = "Edited before removal";
        context.Remove(part);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var record = Assert.Single(capture.Records);
        Assert.Equal(ChangeOperation.Deleted, record.Operation);
        Assert.Equal(Now, record.OccurredAt);
        Assert.Equal(PartKey, record.EntityType);
        Assert.Equal(part.Id.Value, record.EntityId);
        Assert.Equal(WidgetKey, record.SubjectType);
        Assert.Equal(widget.Value, record.SubjectId);
        var values = Values(record);
        Assert.Equal(["label", "widgetId"], values.Keys.Order(StringComparer.Ordinal));
        Assert.All(values.Values, value => Assert.False(value.TryGetProperty("after", out _)));
        Assert.Equal("Spring", values["label"].GetProperty("before").GetString());
        Assert.Equal(widget.Value.ToString("D"), values["widgetId"].GetProperty("before").GetString());
        Assert.Equal(WidgetKey, values["widgetId"].GetProperty("references").GetString());
    }

    [Fact]
    public async Task SaveChangesAsync_AddedEntityFiledUnderAnother_UsesThatEntityAsItsSubject()
    {
        var capture = new CapturingSaveChangesInterceptor();
        using var context = Context(capture);
        var widget = WidgetId.Create();
        var part = new Part { WidgetId = widget, Label = "Bolt" };
        context.Add(part);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var record = Assert.Single(capture.Records);
        Assert.Equal(PartKey, record.EntityType);
        Assert.Equal(part.Id.Value, record.EntityId);
        Assert.Equal(WidgetKey, record.SubjectType);
        Assert.Equal(widget.Value, record.SubjectId);
    }

    [Fact]
    public async Task SaveChangesAsync_UnchangedEntityAndTypesThatAreNotRecorded_RecordNothing()
    {
        var capture = new CapturingSaveChangesInterceptor();
        using var context = Context(capture);
        Persisted(context, new Widget { Name = "Untouched" });
        context.Add(new LogEntry { Id = Guid.CreateVersion7(), Text = "appended" });
        context.Add(new Plain { Id = Guid.CreateVersion7(), Name = "plain" });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(capture.Records);
    }

    [Fact]
    public async Task SaveChangesAsync_ChangesOfSeveralEntities_RecordsEachOnce()
    {
        var capture = new CapturingSaveChangesInterceptor();
        using var context = Context(capture);
        var widget = Persisted(context, new Widget { Name = "One" });
        widget.Name = "Two";
        var category = new Category { Name = "Tools" };
        context.Add(category);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [(CategoryKey, ChangeOperation.Created), (WidgetKey, ChangeOperation.Updated)],
            capture.Records.Select(record => (record.EntityType, record.Operation)).OrderBy(pair => pair.EntityType, StringComparer.Ordinal));
        Assert.Empty(context.ChangeTracker.Entries<ChangeRecord>());
    }

    [Fact]
    public async Task SaveChangesAsync_RecordAddedByOtherCode_IsSavedBesideTheCapturedOne()
    {
        var capture = new CapturingSaveChangesInterceptor();
        using var context = Context(capture);
        var added = ChangeRecord.Record(new ChangeStamp(Earlier, Creator, null, null), ChangeOperation.Updated, WidgetKey, Guid.CreateVersion7(), WidgetKey, Guid.CreateVersion7(), null, 0);
        context.Add(added);
        context.Add(new Widget { Name = "Captured" });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, capture.Records.Count);
        Assert.Contains(added, capture.Records);
        Assert.Equal(EntityState.Added, context.Entry(added).State);
    }

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public async Task SaveChangesAsync_SavedAppendOnlyRowChangedOrRemoved_IsRefused(EntityState state)
    {
        using var context = Context(new CapturingSaveChangesInterceptor());
        var entry = new LogEntry { Id = Guid.CreateVersion7(), Text = "appended" };
        context.Attach(entry);
        context.Entry(entry).State = state;

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("append-only", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveChangesAsync_ContextThatMapsNoChangeRecords_RefusesToSaveARecordedChange()
    {
        using var context = new UnmappedContext(Interceptor());
        context.Add(new UnmappedContext.Ledger { Id = Guid.CreateVersion7() });

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains(nameof(ModuleDbContext), refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveChangesAsync_RecordedTypeWithoutAChangeKey_RefusesToSaveItsChange()
    {
        using var context = new UnkeyedContext(Interceptor(), new CapturingSaveChangesInterceptor());
        context.Add(new UnkeyedContext.Ledger { Id = Guid.CreateVersion7() });

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("RecordChangesAs", refusal.Message, StringComparison.Ordinal);
    }

    private static CaptureContext Context(CapturingSaveChangesInterceptor capture) => new(Interceptor(), capture);

    private static AuditingSaveChangesInterceptor Interceptor() =>
        new(new FixedActorContext(Actor), new FixedRequestOrigin(ClientAddress, CorrelationId), new FakeTimeProvider(Now));

    private static TEntity Persisted<TEntity>(CaptureContext context, TEntity entity)
        where TEntity : class, IAuditable
    {
        context.Attach(entity);

        return entity;
    }

    private static Dictionary<string, JsonElement> Values(ChangeRecord record)
    {
        using var document = JsonDocument.Parse(record.Changes ?? "[]");

        return document.RootElement.EnumerateArray().ToDictionary(value => value.GetProperty("property").GetString()!, value => value.Clone(), StringComparer.Ordinal);
    }

    private static void AssertAfter(JsonElement value, string kind, string after)
    {
        Assert.Equal(kind, value.GetProperty("kind").GetString());
        Assert.Equal(after, value.GetProperty("after").GetString());
    }

    private sealed class FixedActorContext(Guid actorId) : IActorContext
    {
        public Guid ActorId => actorId;

        public bool IsAuthenticated => true;
    }

    private sealed class FixedRequestOrigin(string clientAddress, string correlationId) : IRequestOrigin
    {
        public string? ClientAddress => clientAddress;

        public string? CorrelationId => correlationId;
    }

    private sealed class UnmappedContext(params IInterceptor[] interceptors) : DbContext(new DbContextOptionsBuilder<UnmappedContext>().UseSqlServer().AddInterceptors(interceptors).Options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Ledger>(ledger =>
            {
                ledger.RecordChangesAs("test.capture.ledgers");
                ledger.Property(l => l.Id).ValueGeneratedNever();
            });
        }

        internal sealed class Ledger : IAuditable
        {
            public Guid Id { get; set; }

            public DateTimeOffset CreatedAt { get; set; }

            public Guid CreatedBy { get; set; }

            public DateTimeOffset? ModifiedAt { get; set; }

            public Guid? ModifiedBy { get; set; }
        }
    }

    private sealed class UnkeyedContext(params IInterceptor[] interceptors) : ModuleDbContext(new DbContextOptionsBuilder<UnkeyedContext>().UseSqlServer().AddInterceptors(interceptors).Options, "test_unkeyed")
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Ledger>().Property(l => l.Id).ValueGeneratedNever();
            base.OnModelCreating(modelBuilder);
        }

        internal sealed class Ledger : IAuditable
        {
            public Guid Id { get; set; }

            public DateTimeOffset CreatedAt { get; set; }

            public Guid CreatedBy { get; set; }

            public DateTimeOffset? ModifiedAt { get; set; }

            public Guid? ModifiedBy { get; set; }
        }
    }
}
