using System.Text.RegularExpressions;

namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;

internal static partial class FieldName
{
    public const int MaxLength = 64;

    public static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    public static bool IsValid(string? name) => name is not null && name.Length <= MaxLength && Pattern().IsMatch(name);

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]*$")]
    private static partial Regex Pattern();
}
