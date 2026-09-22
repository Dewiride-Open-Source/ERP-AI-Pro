using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.Conventions;

public sealed class ModelRulesTests
{
    [Fact]
    public void ApplySoftDelete_HierarchyWithSoftDeletableRoot_DeclaresTheFilterOnTheRootOnly()
    {
        using var context = new HierarchyContext();

        var root = context.Model.FindEntityType(typeof(Party))!;
        var derived = context.Model.FindEntityType(typeof(Client))!;

        Assert.Equal(SoftDeleteFilter.Name, Assert.Single(root.GetDeclaredQueryFilters()).Key);
        Assert.Empty(derived.GetDeclaredQueryFilters());
        Assert.Contains("[IsDeleted] = CAST(0 AS bit)", context.Clients.ToQueryString(), StringComparison.Ordinal);
        Assert.DoesNotContain("[IsDeleted] = CAST(0 AS bit)", context.Clients.IncludeDeleted().ToQueryString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyRowVersion_HierarchyWithVersionedRoot_MapsTheInheritedPropertyAsTheConcurrencyToken()
    {
        using var context = new HierarchyContext();

        var property = context.Model.FindEntityType(typeof(Vendor))!.FindProperty(nameof(IVersioned.RowVersion))!;

        Assert.Same(context.Model.FindEntityType(typeof(Party)), property.DeclaringType);
        Assert.True(property.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
        Assert.Equal("rowversion", property.GetColumnType());
    }

    [Fact]
    public void ApplyRowVersion_VersionedDerivedTypeOnly_MapsTheRowVersionOnThatType()
    {
        using var context = new DerivedVersionContext();

        var property = context.Model.FindEntityType(typeof(VersionedNote))!.FindProperty(nameof(IVersioned.RowVersion))!;

        Assert.Same(context.Model.FindEntityType(typeof(VersionedNote)), property.DeclaringType);
        Assert.True(property.IsConcurrencyToken);
        Assert.Null(context.Model.FindEntityType(typeof(Note))!.FindProperty(nameof(IVersioned.RowVersion)));
    }

    [Fact]
    public void ApplySoftDelete_SoftDeletableDerivedTypeOnly_ThrowsNamingTheRoot()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => BuildModel(new DerivedSoftDeleteContext()));

        Assert.Contains(nameof(DeletableNote), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(Note), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplySoftDelete_OwnedSoftDeletableType_ThrowsNamingTheOwnedType()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => BuildModel(new OwnedSoftDeleteContext()));

        Assert.Contains(nameof(Remark), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_ModuleDbContext_DefersCascadeDeletesToSaveChanges()
    {
        using var context = new HierarchyContext();

        Assert.Equal(CascadeTiming.OnSaveChanges, context.ChangeTracker.CascadeDeleteTiming);
    }

    private static IModel BuildModel(DbContext context)
    {
        using (context)
        {
            return context.Model;
        }
    }

    private static DbContextOptions<TContext> Options<TContext>()
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>().UseSqlServer().Options;

    private sealed class HierarchyContext() : ModuleDbContext(Options<HierarchyContext>(), "test_rules")
    {
        public DbSet<Party> Parties => Set<Party>();

        public DbSet<Client> Clients => Set<Client>();

        public DbSet<Vendor> Vendors => Set<Vendor>();
    }

    private sealed class DerivedVersionContext() : ModuleDbContext(Options<DerivedVersionContext>(), "test_rules")
    {
        public DbSet<Note> Notes => Set<Note>();

        public DbSet<VersionedNote> VersionedNotes => Set<VersionedNote>();
    }

    private sealed class DerivedSoftDeleteContext() : ModuleDbContext(Options<DerivedSoftDeleteContext>(), "test_rules")
    {
        public DbSet<Note> Notes => Set<Note>();

        public DbSet<DeletableNote> DeletableNotes => Set<DeletableNote>();
    }

    private sealed class OwnedSoftDeleteContext() : ModuleDbContext(Options<OwnedSoftDeleteContext>(), "test_rules")
    {
        public DbSet<Order> Orders => Set<Order>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>().OwnsOne(o => o.Remark);
            base.OnModelCreating(modelBuilder);
        }
    }

    private abstract class Party : ISoftDeletable, IVersioned
    {
        public Guid Id { get; set; }

        public bool IsDeleted { get; set; }

        public DateTimeOffset? DeletedAt { get; set; }

        public Guid? DeletedBy { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }

    private sealed class Client : Party
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class Vendor : Party
    {
        public string Code { get; set; } = string.Empty;
    }

    private class Note
    {
        public Guid Id { get; set; }
    }

    private sealed class VersionedNote : Note, IVersioned
    {
        public byte[] RowVersion { get; set; } = [];
    }

    private sealed class DeletableNote : Note, ISoftDeletable
    {
        public bool IsDeleted { get; set; }

        public DateTimeOffset? DeletedAt { get; set; }

        public Guid? DeletedBy { get; set; }
    }

    private sealed class Order
    {
        public Guid Id { get; set; }

        public Remark Remark { get; set; } = new();
    }

    private sealed class Remark : ISoftDeletable
    {
        public string Text { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }

        public DateTimeOffset? DeletedAt { get; set; }

        public Guid? DeletedBy { get; set; }
    }
}
