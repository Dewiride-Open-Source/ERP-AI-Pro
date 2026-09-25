using System.Text.RegularExpressions;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;

public static partial class CorrelationId
{
    public const string HeaderName = "X-Correlation-ID";

    public const int MaxLength = 64;

    public static bool IsWellFormed(string? value) => value is not null && value.Length <= MaxLength && Pattern().IsMatch(value);

    // \z rather than $, which would also match before a trailing line feed.
    [GeneratedRegex(@"^[A-Za-z0-9._-]+\z")]
    private static partial Regex Pattern();
}
