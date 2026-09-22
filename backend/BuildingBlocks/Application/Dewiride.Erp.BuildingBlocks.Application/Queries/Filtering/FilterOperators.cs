using System.Collections.Frozen;

namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;

public static class FilterOperators
{
    private static readonly FrozenDictionary<string, FilterOperator> ByToken = new Dictionary<string, FilterOperator>(StringComparer.OrdinalIgnoreCase)
    {
        ["eq"] = FilterOperator.Equal,
        ["ne"] = FilterOperator.NotEqual,
        ["gt"] = FilterOperator.GreaterThan,
        ["gte"] = FilterOperator.GreaterThanOrEqual,
        ["lt"] = FilterOperator.LessThan,
        ["lte"] = FilterOperator.LessThanOrEqual,
        ["contains"] = FilterOperator.Contains,
        ["in"] = FilterOperator.In,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<FilterOperator, string> Tokens = ByToken.ToFrozenDictionary(pair => pair.Value, pair => pair.Key);

    public static readonly IReadOnlyList<FilterOperator> Equality = [FilterOperator.Equal, FilterOperator.NotEqual, FilterOperator.In];

    public static readonly IReadOnlyList<FilterOperator> Comparable = [.. Equality, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual];

    public static readonly IReadOnlyList<FilterOperator> Text = [.. Equality, FilterOperator.Contains];

    public static readonly IReadOnlyList<FilterOperator> Flag = [FilterOperator.Equal, FilterOperator.NotEqual];

    public static bool TryParse(string token, out FilterOperator @operator) => ByToken.TryGetValue(token, out @operator);

    public static string ToToken(this FilterOperator @operator) => Tokens[@operator];
}
