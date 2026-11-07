namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;

// Every text of a set comes from a seed file no person edits in the ERP, so it keeps one shape that a reseed compares exactly:
// no spaces at either end, no two spaces in a row and no control character.
internal static class ParameterText
{
    public static bool IsWellFormed(string? text, int maxLength) =>
        text is { Length: > 0 } && text.Length <= maxLength
        && string.Equals(text, text.Trim(), StringComparison.Ordinal)
        && !text.Contains("  ", StringComparison.Ordinal)
        && !text.Any(IsControl);

    public static string Require(string? text, int maxLength, string what) =>
        IsWellFormed(text, maxLength)
            ? text!
            : throw new ArgumentException($"{what} has 1 to {maxLength} characters, without spaces at either end, two spaces in a row or a control character.", nameof(text));

    private static bool IsControl(char character) => character < ' ' || character == '\u007f';
}
