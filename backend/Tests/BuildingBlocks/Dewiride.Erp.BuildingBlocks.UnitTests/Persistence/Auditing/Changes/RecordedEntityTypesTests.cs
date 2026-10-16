using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.Auditing.Changes;

public sealed class RecordedEntityTypesTests
{
    [Theory]
    [InlineData("identity.users.people", true)]
    [InlineData("platform.attachments.files", true)]
    [InlineData("finance.sales-ai.invoice-lines", true)]
    [InlineData("a1.b2.c3", true)]
    [InlineData("identity.users", false)]
    [InlineData("identity.users.people.roles", false)]
    [InlineData("Identity.users.people", false)]
    [InlineData("identity_users.Users", false)]
    [InlineData("identity..people", false)]
    [InlineData("-identity.users.people", false)]
    [InlineData("identity.users-.people", false)]
    [InlineData("identity.users.1people", false)]
    [InlineData("identity.users.people\n", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsWellFormedKey_Key_IsThreeLowerCaseKebabSegments(string? key, bool wellFormed)
    {
        Assert.Equal(wellFormed, ChangeRecordConventions.IsWellFormedKey(key));
    }

    [Fact]
    public void IsWellFormedKey_KeyLongerThanItsColumn_IsRefused()
    {
        var longest = $"{new string('a', 21)}.{new string('b', 21)}.{new string('c', 20)}";

        Assert.Equal(ChangeRecordMapping.KeyMaxLength, longest.Length);
        Assert.True(ChangeRecordConventions.IsWellFormedKey(longest));
        Assert.False(ChangeRecordConventions.IsWellFormedKey(longest + "c"));
    }

    [Fact]
    public void RecordChangesAs_MalformedKey_FailsTheModel()
    {
        var failure = Assert.Throws<ArgumentException>(() => new MalformedKeyContext());

        Assert.Contains("identity_users.Users", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IsRecorded_EntityType_IsTrueOnlyForAuditableTypesThatAreNotOwned()
    {
        using var context = new CaptureContext();
        using var rules = new RulesContext();

        Assert.True(RecordedEntityTypes.IsRecorded(context.Model.FindEntityType(typeof(CaptureContext.Widget))!));
        Assert.False(RecordedEntityTypes.IsRecorded(context.Model.FindEntityType(typeof(CaptureContext.LogEntry))!));
        Assert.False(RecordedEntityTypes.IsRecorded(context.Model.FindEntityType(typeof(CaptureContext.Plain))!));
        Assert.False(RecordedEntityTypes.IsRecorded(context.Model.FindEntityType(typeof(ChangeRecord))!));
        Assert.False(RecordedEntityTypes.IsRecorded(OwnedNoteType(rules)));
    }

    [Fact]
    public void ProblemsOf_RecordedTypesOfAWellFormedModel_AreNone()
    {
        using var context = new CaptureContext();

        foreach (var entityType in context.Model.GetEntityTypes())
        {
            Assert.Empty(RecordedEntityTypes.ProblemsOf(entityType));
        }
    }

    [Fact]
    public void ProblemsOf_TypeWithoutAChangeKey_AsksForOne()
    {
        Assert.Contains(ProblemsOf<Unkeyed>(), problem => problem.Contains("RecordChangesAs", StringComparison.Ordinal));
    }

    [Fact]
    public void ProblemsOf_KeyTheStoreGenerates_AsksForAGuidSetInCode()
    {
        Assert.Contains(ProblemsOf<Numbered>(), problem => problem.Contains("single key of a Guid provider type that is set in code", StringComparison.Ordinal));
    }

    [Fact]
    public void ProblemsOf_OwnedAuditableType_SaysItsChangesWouldGoUnrecorded()
    {
        using var context = new RulesContext();

        var problem = Assert.Single(RecordedEntityTypes.ProblemsOf(OwnedNoteType(context)));

        Assert.Contains("is owned", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ProblemsOf_OwnedNavigation_SaysTheCaptureCannotRecordIt()
    {
        Assert.Contains(ProblemsOf<Holder>(), problem => problem.Contains("owned navigation", StringComparison.Ordinal));
    }

    [Fact]
    public void ProblemsOf_CascadeFromAPrincipal_AsksToRestrictIt()
    {
        Assert.Contains(ProblemsOf<Child>(), problem => problem.Contains("Cascade delete", StringComparison.Ordinal));
        Assert.Empty(ProblemsOf<Parent>());
    }

    [Fact]
    public void ProblemsOf_SubjectThatIsNotARequiredForeignKey_SaysWhatItMustBe()
    {
        Assert.Contains(ProblemsOf<Filed>(), problem => problem.Contains("required single-property foreign key to a recorded type", StringComparison.Ordinal));
    }

    [Fact]
    public void KeyOf_DerivedType_IsTheKeyOfItsRoot()
    {
        using var context = new RulesContext();

        Assert.Equal("test.rules.bases", RecordedEntityTypes.KeyOf(context.Model.FindEntityType(typeof(Derived))!));
    }

    [Fact]
    public void SubjectKeyOf_TypeFiledUnderAnother_IsItsForeignKeyAndOtherwiseNothing()
    {
        using var context = new CaptureContext();

        var subject = RecordedEntityTypes.SubjectKeyOf(context.Model.FindEntityType(typeof(CaptureContext.Part))!);

        Assert.NotNull(subject);
        Assert.Equal(nameof(CaptureContext.Part.WidgetId), Assert.Single(subject.Properties).Name);
        Assert.Same(context.Model.FindEntityType(typeof(CaptureContext.Widget)), subject.PrincipalEntityType);
        Assert.Null(RecordedEntityTypes.SubjectKeyOf(context.Model.FindEntityType(typeof(CaptureContext.Widget))!));
    }

    private static IReadOnlyList<string> ProblemsOf<TEntity>()
    {
        using var context = new RulesContext();

        return RecordedEntityTypes.ProblemsOf(context.Model.FindEntityType(typeof(TEntity))!);
    }

    private static IEntityType OwnedNoteType(RulesContext context) =>
        context.Model.FindEntityType(typeof(Owner))!.FindNavigation(nameof(Owner.Note))!.TargetEntityType;

    private sealed class RulesContext() : ModuleDbContext(new DbContextOptionsBuilder<RulesContext>().UseSqlServer().Options, "test_rules")
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Unkeyed>().Property(u => u.Id).ValueGeneratedNever();
            modelBuilder.Entity<Numbered>().RecordChangesAs("test.rules.numbered");
            modelBuilder.Entity<Owner>(owner =>
            {
                owner.Property(o => o.Id).ValueGeneratedNever();
                owner.OwnsOne(o => o.Note);
            });
            modelBuilder.Entity<Holder>(holder =>
            {
                holder.RecordChangesAs("test.rules.holders");
                holder.Property(h => h.Id).ValueGeneratedNever();
                holder.OwnsOne(h => h.Detail);
            });
            modelBuilder.Entity<Parent>(parent =>
            {
                parent.RecordChangesAs("test.rules.parents");
                parent.Property(p => p.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<Child>(child =>
            {
                child.RecordChangesAs("test.rules.children");
                child.Property(c => c.Id).ValueGeneratedNever();
                child.HasOne<Parent>().WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<Filed>(filed =>
            {
                filed.RecordChangesAs("test.rules.filed");
                filed.FileChangesUnder(f => f.ParentId);
                filed.Property(f => f.Id).ValueGeneratedNever();
                filed.HasOne<Parent>().WithMany().HasForeignKey(f => f.ParentId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<Base>(root =>
            {
                root.RecordChangesAs("test.rules.bases");
                root.Property(b => b.Id).ValueGeneratedNever();
            });
            modelBuilder.Entity<Derived>();
            base.OnModelCreating(modelBuilder);
        }
    }

    private sealed class MalformedKeyContext() : ModuleDbContext(new DbContextOptionsBuilder<MalformedKeyContext>().UseSqlServer().Options, "test_malformed")
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Unkeyed>().RecordChangesAs("identity_users.Users");
            base.OnModelCreating(modelBuilder);
        }
    }

    private abstract class Audited : IAuditable
    {
        public DateTimeOffset CreatedAt { get; set; }

        public Guid CreatedBy { get; set; }

        public DateTimeOffset? ModifiedAt { get; set; }

        public Guid? ModifiedBy { get; set; }
    }

    private sealed class Unkeyed : Audited
    {
        public Guid Id { get; set; }
    }

    private sealed class Numbered : Audited
    {
        public int Id { get; set; }
    }

    private sealed class Owner
    {
        public Guid Id { get; set; }

        public OwnedNote Note { get; set; } = new();
    }

    private sealed class OwnedNote : Audited
    {
        public string Text { get; set; } = string.Empty;
    }

    private sealed class Holder : Audited
    {
        public Guid Id { get; set; }

        public HolderDetail Detail { get; set; } = new();
    }

    private sealed class HolderDetail
    {
        public string Text { get; set; } = string.Empty;
    }

    private sealed class Parent : Audited
    {
        public Guid Id { get; set; }
    }

    private sealed class Child : Audited
    {
        public Guid Id { get; set; }

        public Guid ParentId { get; set; }
    }

    private sealed class Filed : Audited
    {
        public Guid Id { get; set; }

        public Guid? ParentId { get; set; }
    }

    private class Base : Audited
    {
        public Guid Id { get; set; }
    }

    private sealed class Derived : Base
    {
        public string Extra { get; set; } = string.Empty;
    }
}
