using System.Linq.Expressions;

namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;

public sealed record ResolvedFilter<T>(Expression<Func<T, bool>>? Predicate)
{
    public static readonly ResolvedFilter<T> None = new((Expression<Func<T, bool>>?)null);
}
