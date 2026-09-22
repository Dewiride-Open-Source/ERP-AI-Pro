using System.Linq.Expressions;

namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;

public sealed record ResolvedSortTerm(LambdaExpression Selector, SortDirection Direction);
