using System.Linq.Expressions;

namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;

public sealed record FilterableField(string Name, LambdaExpression Selector, Type ValueType, IReadOnlyList<FilterOperator> Operators);
