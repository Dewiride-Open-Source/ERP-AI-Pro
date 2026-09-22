using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;

public sealed record SortRequest(IReadOnlyList<SortTerm> Terms)
{
    public const char TermSeparator = ',';

    public const char DirectionSeparator = ':';

    public const int MaxTerms = 5;

    public static readonly SortRequest None = new([]);

    public static Result<SortRequest> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return None;
        }

        var terms = new List<SortTerm>();
        foreach (var part in text.Split(TermSeparator, StringSplitOptions.TrimEntries))
        {
            var pieces = part.Split(DirectionSeparator, StringSplitOptions.TrimEntries);
            if (pieces.Length > 2 || !FieldName.IsValid(pieces[0]))
            {
                return QueryErrors.Sort($"'{part}' is not a sort term; use 'field', 'field:asc' or 'field:desc'.");
            }

            var direction = pieces.Length == 1 ? SortDirection.Ascending : ParseDirection(pieces[1]);
            if (direction is null)
            {
                return QueryErrors.Sort($"'{pieces[1]}' is not a sort direction; use 'asc' or 'desc'.");
            }

            if (terms.Any(term => FieldName.Comparer.Equals(term.Field, pieces[0])))
            {
                return QueryErrors.Sort($"'{pieces[0]}' appears more than once.");
            }

            terms.Add(new SortTerm(pieces[0], direction.Value));
        }

        return terms.Count > MaxTerms
            ? QueryErrors.Sort($"At most {MaxTerms} sort terms are allowed.")
            : new SortRequest(terms);
    }

    public Result<ResolvedSort<T>> Resolve<T>(SortableFields<T> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var resolved = new List<ResolvedSortTerm>(Terms.Count);
        foreach (var term in Terms)
        {
            if (!fields.TryGet(term.Field, out var selector))
            {
                return QueryErrors.Field(term.Field, fields.Names);
            }

            resolved.Add(new ResolvedSortTerm(selector, term.Direction));
        }

        return new ResolvedSort<T>(resolved);
    }

    private static SortDirection? ParseDirection(string text) =>
        text.ToUpperInvariant() switch
        {
            "ASC" => SortDirection.Ascending,
            "DESC" => SortDirection.Descending,
            _ => null,
        };
}
