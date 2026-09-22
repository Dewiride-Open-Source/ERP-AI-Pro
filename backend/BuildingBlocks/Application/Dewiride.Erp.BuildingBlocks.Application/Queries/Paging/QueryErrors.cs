using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;

public static class QueryErrors
{
    public const string InvalidPage = "query.invalid-page";

    public const string InvalidSort = "query.invalid-sort";

    public const string InvalidFilter = "query.invalid-filter";

    public const string InvalidField = "query.invalid-field";

    public const string InvalidOperator = "query.invalid-operator";

    public const string InvalidValue = "query.invalid-value";

    public static Error Page(string message) => Error.Validation(InvalidPage, message);

    public static Error Sort(string message) => Error.Validation(InvalidSort, message);

    public static Error Filter(string message) => Error.Validation(InvalidFilter, message);

    public static Error Field(string field, IEnumerable<string> allowed) =>
        Error.Validation(InvalidField, $"'{field}' is not a field this list can sort or filter by; use one of {string.Join(", ", allowed)}.");

    public static Error Operator(string field, string @operator, IEnumerable<string> allowed) =>
        Error.Validation(InvalidOperator, $"'{@operator}' cannot be applied to '{field}'; use one of {string.Join(", ", allowed)}.");

    public static Error Value(string field, string value, Type type) =>
        Error.Validation(InvalidValue, $"'{value}' is not a valid {type.Name} for '{field}'.");
}
