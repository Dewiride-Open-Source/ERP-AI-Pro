namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;

public sealed record ResolvedSort<T>(IReadOnlyList<ResolvedSortTerm> Terms)
{
    public static readonly ResolvedSort<T> None = new([]);
}
