using System.Linq.Expressions;

namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;

public static class SortQueryableExtensions
{
    public static IOrderedQueryable<T> ApplySort<T, TKey>(this IQueryable<T> source, ResolvedSort<T> sort, Expression<Func<T, TKey>> tieBreaker)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sort);
        ArgumentNullException.ThrowIfNull(tieBreaker);

        var ordered = false;
        var query = source;
        foreach (var term in sort.Terms)
        {
            query = Order(query, term.Selector, term.Direction, ordered);
            ordered = true;
        }

        return (IOrderedQueryable<T>)Order(query, tieBreaker, SortDirection.Ascending, ordered);
    }

    private static IQueryable<T> Order<T>(IQueryable<T> source, LambdaExpression selector, SortDirection direction, bool alreadyOrdered)
    {
        var method = (alreadyOrdered, direction) switch
        {
            (false, SortDirection.Ascending) => nameof(Queryable.OrderBy),
            (false, _) => nameof(Queryable.OrderByDescending),
            (true, SortDirection.Ascending) => nameof(Queryable.ThenBy),
            (true, _) => nameof(Queryable.ThenByDescending),
        };
        var call = Expression.Call(typeof(Queryable), method, [typeof(T), selector.ReturnType], source.Expression, Expression.Quote(selector));

        return source.Provider.CreateQuery<T>(call);
    }
}
