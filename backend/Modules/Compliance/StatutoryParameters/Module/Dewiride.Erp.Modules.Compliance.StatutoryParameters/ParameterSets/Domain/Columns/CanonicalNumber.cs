using System.Globalization;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;

// A number is kept as text, never as a JSON number, because SQL Server may round a JSON number it converts into its json type.
// The text drops trailing zeros and never uses an exponent, so 2.50 and 2.5 are one value with one text, "2.5"; 28 places is a
// decimal's largest scale.
internal static class CanonicalNumber
{
    private static readonly string Format = "0." + new string('#', 28);

    public static string Of(decimal value) => value.ToString(Format, CultureInfo.InvariantCulture);

    public static bool TryParse(string? text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)
        && string.Equals(Of(value), text, StringComparison.Ordinal);

    public static int DecimalsOf(decimal value)
    {
        var text = Of(value);
        var point = text.IndexOf('.', StringComparison.Ordinal);

        return point < 0 ? 0 : text.Length - point - 1;
    }
}
