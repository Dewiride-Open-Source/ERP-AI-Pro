using System.Text.RegularExpressions;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Definitions;

internal static partial class JobNames
{
    public const int MaxLength = 128;

    public static bool IsWellFormed(string? name) => name is not null && name.Length <= MaxLength && Pattern().IsMatch(name);

    // \z rather than $, which would also match before a trailing line feed.
    [GeneratedRegex(@"^[a-z][a-z0-9]*(-[a-z0-9]+)*(\.[a-z][a-z0-9]*(-[a-z0-9]+)*){2}\z")]
    private static partial Regex Pattern();
}
