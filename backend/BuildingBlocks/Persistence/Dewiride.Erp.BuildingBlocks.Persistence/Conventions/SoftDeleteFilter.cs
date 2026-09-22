using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Conventions;

public static class SoftDeleteFilter
{
    public const string Name = "SoftDelete";

    public static IQueryable<TEntity> IncludeDeleted<TEntity>(this IQueryable<TEntity> query)
        where TEntity : class, ISoftDeletable =>
        query.IgnoreQueryFilters([Name]);
}
