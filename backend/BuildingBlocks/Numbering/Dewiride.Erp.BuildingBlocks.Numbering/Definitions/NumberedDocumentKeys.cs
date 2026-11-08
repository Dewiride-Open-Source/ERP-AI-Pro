using System.Text.RegularExpressions;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Definitions;

internal static partial class NumberedDocumentKeys
{
    public const int MaxLength = 128;

    public static bool IsWellFormed(string? key) => key is not null && key.Length <= MaxLength && Pattern().IsMatch(key);

    // \z rather than $, which would also match before a trailing line feed.
    [GeneratedRegex(@"^[a-z][a-z0-9]*(-[a-z0-9]+)*(\.[a-z][a-z0-9]*(-[a-z0-9]+)*){2}\z")]
    private static partial Regex Pattern();
}
