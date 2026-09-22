using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;

public sealed record FilterRequest(IReadOnlyList<FilterTerm> Terms)
{
    public const char TermSeparator = ';';

    public const char PartSeparator = ':';

    public const char ValueSeparator = '|';

    public const int MaxTerms = 10;

    public const int MaxValuesPerTerm = 100;

    public static readonly FilterRequest None = new([]);

    public static Result<FilterRequest> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return None;
        }

        var terms = new List<FilterTerm>();
        foreach (var part in text.Split(TermSeparator, StringSplitOptions.TrimEntries))
        {
            var pieces = part.Split(PartSeparator, 3, StringSplitOptions.TrimEntries);
            if (pieces.Length != 3 || !FieldName.IsValid(pieces[0]))
            {
                return QueryErrors.Filter($"'{part}' is not a filter term; use 'field:operator:value'.");
            }

            if (!FilterOperators.TryParse(pieces[1], out var @operator))
            {
                return QueryErrors.Filter($"'{pieces[1]}' is not a filter operator; use eq, ne, gt, gte, lt, lte, contains or in.");
            }

            var values = @operator == FilterOperator.In
                ? pieces[2].Split(ValueSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Uri.UnescapeDataString).ToArray()
                : [Uri.UnescapeDataString(pieces[2])];
            if (values.Length == 0 || values.Length > MaxValuesPerTerm)
            {
                return QueryErrors.Filter($"'{pieces[0]}:in' needs between 1 and {MaxValuesPerTerm} values separated by '{ValueSeparator}'.");
            }

            terms.Add(new FilterTerm(pieces[0], @operator, values));
        }

        return terms.Count > MaxTerms
            ? QueryErrors.Filter($"At most {MaxTerms} filter terms are allowed.")
            : new FilterRequest(terms);
    }

    public Result<ResolvedFilter<T>> Resolve<T>(FilterableFields<T> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var builder = new FilterPredicateBuilder<T>();
        foreach (var term in Terms)
        {
            if (!fields.TryGet(term.Field, out var field))
            {
                return QueryErrors.Field(term.Field, fields.Names);
            }

            if (!field.Operators.Contains(term.Operator))
            {
                return QueryErrors.Operator(term.Field, term.Operator.ToToken(), field.Operators.Select(FilterOperators.ToToken));
            }

            var values = new List<object?>(term.Values.Count);
            foreach (var text in term.Values)
            {
                if (!FieldValueParser.TryParse(text, field.ValueType, out var value))
                {
                    return QueryErrors.Value(term.Field, text, Nullable.GetUnderlyingType(field.ValueType) ?? field.ValueType);
                }

                values.Add(value);
            }

            builder.Add(field, term.Operator, values);
        }

        return builder.Build();
    }
}
