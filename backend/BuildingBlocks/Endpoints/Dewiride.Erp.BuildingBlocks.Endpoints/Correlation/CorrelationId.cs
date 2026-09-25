using System.Text.RegularExpressions;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;

public static partial class CorrelationId
{
    public const string HeaderName = "X-Correlation-ID";

    public const int MaxLength = 64;

    public static bool IsWellFormed(string? value) => value is not null && value.Length <= MaxLength && Pattern().IsMatch(value);

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex Pattern();
}
