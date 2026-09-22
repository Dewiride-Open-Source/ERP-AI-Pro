using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.Auditing;

public sealed class AuditingSaveChangesInterceptorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Earlier = new(2026, 1, 15, 8, 30, 0, TimeSpan.Zero);

    private static readonly Guid Actor = new("0199a1b2-0000-7000-8000-0000000000aa");

    private static readonly Guid Creator = new("0199a1b2-0000-7000-8000-0000000000bb");

    [Fact]
    public void Stamp_AddedAuditable_SetsTheCreatedStampsAndClearsTheModifiedStamps()
    {
        using var context = new AuditContext();
        var document = new Document { ModifiedAt = Earlier, ModifiedBy = Creator };
        context.Add(document);

        AuditingSaveChangesInterceptor.Stamp(context.ChangeTracker, Now, Actor);

        Assert.Equal(EntityState.Added, context.Entry(document).State);
        Assert.Equal(Now, document.CreatedAt);
        Assert.Equal(Actor, document.CreatedBy);
        Assert.Null(document.ModifiedAt);
        Assert.Null(document.ModifiedBy);
        Assert.False(document.IsDeleted);
        Assert.Null(document.DeletedAt);
        Assert.Null(document.DeletedBy);
    }

    [Fact]
    public void Stamp_ModifiedAuditable_SetsTheModifiedStampsAndLeavesTheCreatedStampsUntouched()
    {
        using var context = new AuditContext();
        var document = Persisted(context);
        document.Name = "renamed";

        AuditingSaveChangesInterceptor.Stamp(context.ChangeTracker, Now, Actor);

        var entry = context.Entry(document);
        Assert.Equal(EntityState.Modified, entry.State);
        Assert.Equal(Now, document.ModifiedAt);
        Assert.Equal(Actor, document.ModifiedBy);
        Assert.Equal(Earlier, document.CreatedAt);
        Assert.Equal(Creator, document.CreatedBy);
        Assert.True(entry.Property(nameof(Document.ModifiedAt)).IsModified);
        Assert.True(entry.Property(nameof(Document.ModifiedBy)).IsModified);
        Assert.False(entry.Property(nameof(Document.CreatedAt)).IsModified);
        Assert.False(entry.Property(nameof(Document.CreatedBy)).IsModified);
        Assert.False(document.IsDeleted);
    }

    [Fact]
    public void Stamp_ModifiedAuditableWithTamperedCreatedStamps_UnmarksTheCreatedStampsForTheUpdate()
    {
        using var context = new AuditContext();
        var document = Persisted(context);
        document.CreatedAt = Now;
        document.CreatedBy = Actor;
        context.ChangeTracker.DetectChanges();
        Assert.True(context.Entry(document).Property(nameof(Document.CreatedAt)).IsModified);

        AuditingSaveChangesInterceptor.Stamp(context.ChangeTracker, Now, Actor);

        var entry = context.Entry(document);
        Assert.False(entry.Property(nameof(Document.CreatedAt)).IsModified);
        Assert.False(entry.Property(nameof(Document.CreatedBy)).IsModified);
        Assert.Equal(Earlier, entry.Property(nameof(Document.CreatedAt)).OriginalValue);
        Assert.Equal(Creator, entry.Property(nameof(Document.CreatedBy)).OriginalValue);
        Assert.Equal(Now, document.ModifiedAt);
        Assert.Equal(Actor, document.ModifiedBy);
    }

    [Fact]
    public void Stamp_DeletedSoftDeletableAuditable_BecomesAnUpdateCarryingTheDeleteAndModifiedStamps()
    {
        using var context = new AuditContext();
        var document = Persisted(context);
        context.Remove(document);

        AuditingSaveChangesInterceptor.Stamp(context.ChangeTracker, Now, Actor);

        var entry = context.Entry(document);
        Assert.Equal(EntityState.Modified, entry.State);
        Assert.True(document.IsDeleted);
        Assert.Equal(Now, document.DeletedAt);
        Assert.Equal(Actor, document.DeletedBy);
        Assert.Equal(Now, document.ModifiedAt);
        Assert.Equal(Actor, document.ModifiedBy);
        Assert.Equal(Earlier, document.CreatedAt);
        Assert.Equal(Creator, document.CreatedBy);
        Assert.False(entry.Property(nameof(Document.CreatedAt)).IsModified);
        Assert.False(entry.Property(nameof(Document.CreatedBy)).IsModified);
        Assert.True(entry.Property(nameof(Document.IsDeleted)).IsModified);
    }

    [Fact]
    public void Stamp_AddedSoftDeletableCarryingDeleteStamps_ResetsThemToLive()
    {
        using var context = new AuditContext();
        var document = new Document { IsDeleted = true, DeletedAt = Earlier, DeletedBy = Creator };
        context.Add(document);

        AuditingSaveChangesInterceptor.Stamp(context.ChangeTracker, Now, Actor);

        Assert.False(document.IsDeleted);
        Assert.Null(document.DeletedAt);
        Assert.Null(document.DeletedBy);
    }

    [Fact]
    public void Stamp_ModifiedSoftDeletableWithTamperedDeleteStamps_UnmarksTheDeleteStampsForTheUpdate()
    {
        using var context = new AuditContext();
        var document = Persisted(context);
        document.Name = "renamed";
        document.DeletedAt = Now;
        document.DeletedBy = Actor;
        context.ChangeTracker.DetectChanges();
        Assert.True(context.Entry(document).Property(nameof(Document.DeletedAt)).IsModified);

        AuditingSaveChangesInterceptor.Stamp(context.ChangeTracker, Now, Actor);

        var entry = context.Entry(document);
        Assert.False(entry.Property(nameof(Document.IsDeleted)).IsModified);
        Assert.False(entry.Property(nameof(Document.DeletedAt)).IsModified);
        Assert.False(entry.Property(nameof(Document.DeletedBy)).IsModified);
        Assert.True(entry.Property(nameof(Document.Name)).IsModified);
    }

    [Fact]
    public void Stamp_UpdatedDetachedCopyOfADeletedRow_CannotResurrectIt()
    {
        using var context = new AuditContext();
        var copy = new Document { Id = Guid.CreateVersion7(), Name = "copy", CreatedAt = Earlier, CreatedBy = Creator };
        context.Update(copy);

        AuditingSaveChangesInterceptor.Stamp(context.ChangeTracker, Now, Actor);

        var entry = context.Entry(copy);
        Assert.Equal(EntityState.Modified, entry.State);
        Assert.False(entry.Property(nameof(Document.IsDeleted)).IsModified);
        Assert.False(entry.Property(nameof(Document.DeletedAt)).IsModified);
        Assert.False(entry.Property(nameof(Document.DeletedBy)).IsModified);
    }

    [Fact]
    public void Stamp_ModifiedSoftDeletableFlaggedDeletedByDomainCode_StampsTheDeletionLikeARemove()
    {
        using var context = new AuditContext();
        var document = Persisted(context);
        document.IsDeleted = true;

        AuditingSaveChangesInterceptor.Stamp(context.ChangeTracker, Now, Actor);

        Assert.Equal(EntityState.Modified, context.Entry(document).State);
        Assert.Equal(Now, document.DeletedAt);
        Assert.Equal(Actor, document.DeletedBy);
        Assert.Equal(Now, document.ModifiedAt);
        Assert.Equal(Actor, document.ModifiedBy);
    }

    [Fact]
    public void Stamp_ModifiedSoftDeletableRestoredByDomainCode_ClearsTheDeleteStamps()
    {
        using var context = new AuditContext();
        var document = PersistedDeleted(context);
        document.IsDeleted = false;

        AuditingSaveChangesInterceptor.Stamp(context.ChangeTracker, Now, Actor);

        var entry = context.Entry(document);
        Assert.Equal(EntityState.Modified, entry.State);
        Assert.Null(document.DeletedAt);
        Assert.Null(document.DeletedBy);
        Assert.True(entry.Property(nameof(Document.DeletedAt)).IsModified);
        Assert.Equal(Now, document.ModifiedAt);
        Assert.Equal(Actor, document.ModifiedBy);
    }

    [Fact]
    public void Stamp_DeletedAlreadySoftDeletedEntity_LeavesTheRowAndItsStampsUntouched()
    {
        using var context = new AuditContext();
        var document = PersistedDeleted(context);
        context.Remove(document);

        AuditingSaveChangesInterceptor.Stamp(context.ChangeTracker, Now, Actor);

        Assert.Equal(EntityState.Unchanged, context.Entry(document).State);
        Assert.True(document.IsDeleted);
        Assert.Equal(Earlier, document.DeletedAt);
        Assert.Equal(Creator, document.DeletedBy);
        Assert.Null(document.ModifiedAt);
    }

    [Fact]
    public void Stamp_DeletedSoftDeletableWithoutAuditing_BecomesAnUpdateCarryingOnlyTheDeleteStamps()
    {
        using var context = new AuditContext();
        var tag = new Tag { Id = Guid.CreateVersion7() };
        context.Attach(tag);
        context.Remove(tag);

        AuditingSaveChangesInterceptor.Stamp(context.ChangeTracker, Now, Actor);

        Assert.Equal(EntityState.Modified, context.Entry(tag).State);
        Assert.True(tag.IsDeleted);
        Assert.Equal(Now, tag.DeletedAt);
        Assert.Equal(Actor, tag.DeletedBy);
    }

    [Fact]
    public void Stamp_DeletedAuditableWithoutSoftDelete_StaysAHardDeleteWithoutStamps()
    {
        using var context = new AuditContext();
        var note = new Note { Id = Guid.CreateVersion7(), CreatedAt = Earlier, CreatedBy = Creator };
        context.Attach(note);
        context.Remove(note);

        AuditingSaveChangesInterceptor.Stamp(context.ChangeTracker, Now, Actor);

        Assert.Equal(EntityState.Deleted, context.Entry(note).State);
        Assert.Equal(Earlier, note.CreatedAt);
        Assert.Equal(Creator, note.CreatedBy);
        Assert.Null(note.ModifiedAt);
        Assert.Null(note.ModifiedBy);
    }

    [Fact]
    public void Stamp_UnchangedAuditable_IsLeftAlone()
    {
        using var context = new AuditContext();
        var document = Persisted(context);

        AuditingSaveChangesInterceptor.Stamp(context.ChangeTracker, Now, Actor);

        Assert.Equal(EntityState.Unchanged, context.Entry(document).State);
        Assert.Equal(Earlier, document.CreatedAt);
        Assert.Equal(Creator, document.CreatedBy);
        Assert.Null(document.ModifiedAt);
        Assert.Null(document.ModifiedBy);
        Assert.False(document.IsDeleted);
    }

    [Fact]
    public void Stamp_EntitiesWithoutAuditingOrSoftDelete_AreLeftAlone()
    {
        using var context = new AuditContext();
        var added = new Plain { Id = Guid.CreateVersion7(), Name = "added" };
        var modified = new Plain { Id = Guid.CreateVersion7(), Name = "original" };
        var deleted = new Plain { Id = Guid.CreateVersion7(), Name = "deleted" };
        context.Add(added);
        context.Attach(modified);
        context.Attach(deleted);
        modified.Name = "changed";
        context.Remove(deleted);

        AuditingSaveChangesInterceptor.Stamp(context.ChangeTracker, Now, Actor);

        Assert.Equal(EntityState.Added, context.Entry(added).State);
        Assert.Equal(EntityState.Modified, context.Entry(modified).State);
        Assert.Equal(EntityState.Deleted, context.Entry(deleted).State);
        Assert.Equal("changed", modified.Name);
    }

    [Fact]
    public void Stamp_MixedChangeSet_StampsEveryEntryByItsOwnState()
    {
        using var context = new AuditContext();
        var added = new Document();
        var modified = Persisted(context);
        var deleted = Persisted(context);
        context.Add(added);
        modified.Name = "changed";
        context.Remove(deleted);

        AuditingSaveChangesInterceptor.Stamp(context.ChangeTracker, Now, Actor);

        Assert.Equal(Now, added.CreatedAt);
        Assert.Null(added.ModifiedAt);
        Assert.Equal(Now, modified.ModifiedAt);
        Assert.False(modified.IsDeleted);
        Assert.True(deleted.IsDeleted);
        Assert.Equal(EntityState.Modified, context.Entry(deleted).State);
    }

    [Fact]
    public void Stamp_NullChangeTracker_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AuditingSaveChangesInterceptor.Stamp(null!, Now, Actor));
    }

    [Fact]
    public void SaveChanges_SynchronousCall_ThrowsNotSupportedException()
    {
        using var context = new AuditContext(Interceptor());
        context.Add(new Document());

        var exception = Assert.Throws<NotSupportedException>(() => context.SaveChanges());

        Assert.Contains("SaveChangesAsync", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SavingChanges_DirectCall_ThrowsNotSupportedException()
    {
        var interceptor = Interceptor();

        Assert.Throws<NotSupportedException>(() => interceptor.SavingChanges(null!, default));
    }

    [Fact]
    public async Task SaveChangesAsync_AddedAuditable_IsStampedWithTheActorAndClockBeforeTheWrite()
    {
        using var context = new AuditContext(Interceptor(), new SuppressingSaveChangesInterceptor());
        var document = new Document();
        context.Add(document);

        var written = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, written);
        Assert.Equal(Now, document.CreatedAt);
        Assert.Equal(Actor, document.CreatedBy);
        Assert.Null(document.ModifiedAt);
        Assert.Null(document.ModifiedBy);
    }

    [Fact]
    public async Task SaveChangesAsync_SoftDeletedEntity_IsStampedThroughTheRegisteredInterceptor()
    {
        using var context = new AuditContext(Interceptor(), new SuppressingSaveChangesInterceptor());
        var document = Persisted(context);
        context.Remove(document);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(document.IsDeleted);
        Assert.Equal(Now, document.DeletedAt);
        Assert.Equal(Actor, document.DeletedBy);
        Assert.Equal(EntityState.Modified, context.Entry(document).State);
    }

    [Fact]
    public async Task SavingChangesAsync_NullEventData_ThrowsArgumentNullException()
    {
        var interceptor = Interceptor();

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await interceptor.SavingChangesAsync(null!, default, TestContext.Current.CancellationToken));
    }

    private static AuditingSaveChangesInterceptor Interceptor() => new(new FixedActorContext(Actor), new FakeTimeProvider(Now));

    private static Document PersistedDeleted(AuditContext context)
    {
        var document = new Document { Id = Guid.CreateVersion7(), Name = "gone", CreatedAt = Earlier, CreatedBy = Creator, IsDeleted = true, DeletedAt = Earlier, DeletedBy = Creator };
        context.Attach(document);

        return document;
    }

    private static Document Persisted(AuditContext context)
    {
        var document = new Document { Id = Guid.CreateVersion7(), Name = "original", CreatedAt = Earlier, CreatedBy = Creator };
        context.Attach(document);

        return document;
    }

    private sealed class FixedActorContext(Guid actorId) : IActorContext
    {
        public Guid ActorId => actorId;

        public bool IsAuthenticated => true;
    }

    private sealed class SuppressingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
    }

    private sealed class AuditContext(params IInterceptor[] interceptors) : DbContext(Options(interceptors))
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Document>();
            modelBuilder.Entity<Tag>();
            modelBuilder.Entity<Note>();
            modelBuilder.Entity<Plain>();
        }

        private static DbContextOptions<AuditContext> Options(IInterceptor[] interceptors) =>
            new DbContextOptionsBuilder<AuditContext>().UseSqlServer().AddInterceptors(interceptors).Options;
    }

    private sealed class Document : IAuditable, ISoftDeletable
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public string Name { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        public Guid CreatedBy { get; set; }

        public DateTimeOffset? ModifiedAt { get; set; }

        public Guid? ModifiedBy { get; set; }

        public bool IsDeleted { get; set; }

        public DateTimeOffset? DeletedAt { get; set; }

        public Guid? DeletedBy { get; set; }
    }

    private sealed class Tag : ISoftDeletable
    {
        public Guid Id { get; set; }

        public bool IsDeleted { get; set; }

        public DateTimeOffset? DeletedAt { get; set; }

        public Guid? DeletedBy { get; set; }
    }

    private sealed class Note : IAuditable
    {
        public Guid Id { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public Guid CreatedBy { get; set; }

        public DateTimeOffset? ModifiedAt { get; set; }

        public Guid? ModifiedBy { get; set; }
    }

    private sealed class Plain
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
