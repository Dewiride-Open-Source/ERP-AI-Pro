using System.Linq.Expressions;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Conventions;

public static class ModelRules
{
    public static ModelBuilder ApplySoftDelete(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(Introduces<ISoftDeletable>).ToList())
        {
            if (entityType.IsOwned())
            {
                throw new InvalidOperationException($"Owned type {entityType.DisplayName()} cannot implement ISoftDeletable; soft delete belongs to the entity that owns it.");
            }

            if (entityType.BaseType is not null)
            {
                throw new InvalidOperationException($"{entityType.DisplayName()} implements ISoftDeletable but its root {entityType.GetRootType().DisplayName()} does not; a query filter applies to the root of a hierarchy, so the root must implement it.");
            }

            var entity = Expression.Parameter(entityType.ClrType, "entity");
            var notDeleted = Expression.Lambda(Expression.Not(Expression.Property(entity, nameof(ISoftDeletable.IsDeleted))), entity);
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(SoftDeleteFilter.Name, notDeleted);
        }

        return modelBuilder;
    }

    public static ModelBuilder ApplyRowVersion(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(Introduces<IVersioned>).ToList())
        {
            if (entityType.IsOwned())
            {
                throw new InvalidOperationException($"Owned type {entityType.DisplayName()} cannot implement IVersioned; the row version belongs to the entity that owns it.");
            }

            modelBuilder.Entity(entityType.ClrType).Property(nameof(IVersioned.RowVersion)).IsRowVersion();
        }

        return modelBuilder;
    }

    private static bool Introduces<TInterface>(IMutableEntityType entityType) =>
        typeof(TInterface).IsAssignableFrom(entityType.ClrType)
        && (entityType.BaseType is null || !typeof(TInterface).IsAssignableFrom(entityType.BaseType.ClrType));
}
