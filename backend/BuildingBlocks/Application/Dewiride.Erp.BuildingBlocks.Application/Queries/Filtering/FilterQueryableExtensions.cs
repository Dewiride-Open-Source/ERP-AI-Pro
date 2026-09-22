namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;

public static class FilterQueryableExtensions
{
    public static IQueryable<T> ApplyFilter<T>(this IQueryable<T> source, ResolvedFilter<T> filter)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(filter);

        return filter.Predicate is null ? source : source.Where(filter.Predicate);
    }
}
