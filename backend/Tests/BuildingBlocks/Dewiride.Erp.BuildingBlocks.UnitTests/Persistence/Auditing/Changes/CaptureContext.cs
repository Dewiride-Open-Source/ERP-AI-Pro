using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.Auditing.Changes;

internal sealed class CaptureContext(params IInterceptor[] interceptors) : ModuleDbContext(Options(interceptors), SchemaName)
{
    public const string SchemaName = "test_capture";

    public const string WidgetKey = "test.capture.widgets";

    public const string CategoryKey = "test.capture.categories";

    public const string PartKey = "test.capture.parts";

    internal enum Colour : byte
    {
        Red = 1,
        DeepBlue = 2,
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Widget>(widget =>
        {
            widget.RecordChangesAs(WidgetKey);
            widget.Property(w => w.Name).HasMaxLength(50);
            widget.ComplexProperty(w => w.Size);
            widget.Property(w => w.Colour).HasConversion<byte>();
            widget.PrimitiveCollection(w => w.Finishes).ElementType().HasConversion<string>();
            widget.Property(w => w.Secret).ExcludeFromChangeRecords();
            widget.HasOne<Category>().WithMany().HasForeignKey(w => w.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Category>().RecordChangesAs(CategoryKey);
        modelBuilder.Entity<Part>(part =>
        {
            part.RecordChangesAs(PartKey);
            part.FileChangesUnder(p => p.WidgetId);
            part.HasOne<Widget>().WithMany().HasForeignKey(p => p.WidgetId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<LogEntry>();
        modelBuilder.Entity<Plain>();
        base.OnModelCreating(modelBuilder);
    }

    private static DbContextOptions<CaptureContext> Options(IInterceptor[] interceptors) =>
        new DbContextOptionsBuilder<CaptureContext>().UseSqlServer().AddInterceptors(interceptors).Options;

    internal readonly record struct WidgetId(Guid Value) : IStronglyTypedId<WidgetId>
    {
        public static WidgetId Create() => new(Guid.CreateVersion7());

        public static WidgetId From(Guid value) => new(value);
    }

    internal readonly record struct CategoryId(Guid Value) : IStronglyTypedId<CategoryId>
    {
        public static CategoryId Create() => new(Guid.CreateVersion7());

        public static CategoryId From(Guid value) => new(value);
    }

    internal readonly record struct PartId(Guid Value) : IStronglyTypedId<PartId>
    {
        public static PartId Create() => new(Guid.CreateVersion7());

        public static PartId From(Guid value) => new(value);
    }

    internal readonly record struct Size(decimal Width, decimal Height);

    internal sealed class Widget : IAuditable, ISoftDeletable, IVersioned
    {
        public WidgetId Id { get; set; } = WidgetId.Create();

        public string Name { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public Money Price { get; set; } = new(0m, Currency.Inr);

        public Size Size { get; set; }

        public Colour Colour { get; set; } = Colour.Red;

        public List<Colour> Finishes { get; set; } = [];

        public DateOnly? ReleasedOn { get; set; }

        public DateTimeOffset? InspectedAt { get; set; }

        public bool IsFeatured { get; set; }

        public Guid? ExternalId { get; set; }

        public byte[]? Thumbprint { get; set; }

        public string? Secret { get; set; }

        public CategoryId? CategoryId { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public Guid CreatedBy { get; set; }

        public DateTimeOffset? ModifiedAt { get; set; }

        public Guid? ModifiedBy { get; set; }

        public bool IsDeleted { get; set; }

        public DateTimeOffset? DeletedAt { get; set; }

        public Guid? DeletedBy { get; set; }

        public byte[] RowVersion { get; set; } = [1, 2, 3, 4, 5, 6, 7, 8];
    }

    internal sealed class Category : IAuditable, ISoftDeletable
    {
        public CategoryId Id { get; set; } = CategoryId.Create();

        public string Name { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        public Guid CreatedBy { get; set; }

        public DateTimeOffset? ModifiedAt { get; set; }

        public Guid? ModifiedBy { get; set; }

        public bool IsDeleted { get; set; }

        public DateTimeOffset? DeletedAt { get; set; }

        public Guid? DeletedBy { get; set; }
    }

    internal sealed class Part : IAuditable
    {
        public PartId Id { get; set; } = PartId.Create();

        public WidgetId WidgetId { get; set; }

        public string Label { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        public Guid CreatedBy { get; set; }

        public DateTimeOffset? ModifiedAt { get; set; }

        public Guid? ModifiedBy { get; set; }
    }

    internal sealed class LogEntry : IAppendOnly
    {
        public Guid Id { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    internal sealed class Plain
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
