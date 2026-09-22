namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;

public sealed record FilterTerm(string Field, FilterOperator Operator, IReadOnlyList<string> Values);
