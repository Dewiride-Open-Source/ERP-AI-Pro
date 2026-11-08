using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Numbering.Rules;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Domain;

// A number is the prefix, then the financial year and the separator when the year is shown, then the running number padded with
// zeros to Digits; a running number longer than Digits keeps all its digits. Every number begins with a capital letter or a digit
// from 1 to 9, because the Invoice Registration Portal refuses an e-invoice whose number begins with "0", "/" or "-" (Generate
// IRN API 1.03, validation 8), and its letters are capitals, because the portal and GSTR-1 tell numbers apart without regard to
// case, so a number reads the same in the ERP as in every return that carries it.
public readonly record struct NumberFormat(string Prefix, NumberYearStyle Year, string Separator, int Digits)
{
    public const int PrefixMaxLength = 32;

    public const int SeparatorMaxLength = 1;

    public const int MaxDigits = 18;

    public const string FormatField = "Format";

    public const string PrefixField = "Format.Prefix";

    public const string YearField = "Format.Year";

    public const string SeparatorField = "Format.Separator";

    public const string DigitsField = "Format.Digits";

    private const char YearHyphen = '-';

    private const int FullYearLength = 7;

    private const int AbbreviatedYearLength = 5;

    public int FixedLength => Prefix.Length + YearLength + (Year == NumberYearStyle.None ? 0 : Separator.Length);

    private int YearLength => Year switch
    {
        NumberYearStyle.Full => FullYearLength,
        NumberYearStyle.Abbreviated => AbbreviatedYearLength,
        _ => 0,
    };

    public string Render(FinancialYear year, long number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);

        var running = number.ToString(CultureInfo.InvariantCulture).PadLeft(Digits, '0');
        return Year switch
        {
            NumberYearStyle.Full => string.Concat(Prefix, year.Label, Separator, running),
            NumberYearStyle.Abbreviated => string.Concat(Prefix, year.Label.AsSpan(2), Separator, running),
            _ => Prefix + running,
        };
    }

    // The last running number, of at most MaxDigits digits, whose number is at most maxLength characters long; 0 when not even the
    // first one fits.
    public long LastNumberWithin(int maxLength)
    {
        var room = maxLength - FixedLength;
        if (room < Digits || room < 1)
        {
            return 0;
        }

        var limit = 1L;
        for (var digit = 0; digit < Math.Min(room, MaxDigits); digit++)
        {
            limit *= 10;
        }

        return limit - 1;
    }

    // Each problem is keyed by the member it concerns, so an API answers it as a validation problem of that field.
    public IReadOnlyDictionary<string, string[]> ProblemsUnder(SerialNumberRule rule, FinancialYear year)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var problems = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (Prefix.Length > PrefixMaxLength || !Prefix.All(character => IsAllowed(character, rule)))
        {
            problems[PrefixField] = [$"Use at most {PrefixMaxLength} characters, each a capital letter, a digit or {Describe(rule.SpecialCharacters)}."];
        }

        if (!Enum.IsDefined(Year))
        {
            problems[YearField] = ["Choose how the financial year is shown, or that it is not shown."];
        }
        else if (Year != NumberYearStyle.None && !rule.SpecialCharacters.Contains(YearHyphen, StringComparison.Ordinal))
        {
            problems[YearField] = [$"The financial year is written with \"{YearHyphen}\", which a number may not contain, so leave the year out."];
        }

        if (Separator.Length > SeparatorMaxLength || (Separator.Length == 1 && !rule.SpecialCharacters.Contains(Separator[0], StringComparison.Ordinal)))
        {
            problems[SeparatorField] = [$"Use {Describe(rule.SpecialCharacters)}, or nothing."];
        }
        else if (Year == NumberYearStyle.None && Separator.Length > 0)
        {
            problems[SeparatorField] = ["The separator follows the financial year, so leave it empty when the year is not shown."];
        }

        if (Digits is < 1 or > MaxDigits)
        {
            problems[DigitsField] = [$"Use 1 to {MaxDigits} digits for the running number."];
        }

        if (problems.Count > 0)
        {
            return problems;
        }

        var first = Render(year, 1);
        if (first.Length > rule.MaxLength)
        {
            problems[FormatField] = [$"The first number, {first}, has {first.Length} characters; a number may have at most {rule.MaxLength}."];
        }
        else if (!IsLetterOrNonZeroNumeral(first[0]))
        {
            var field = Prefix.Length > 0 ? PrefixField : Year == NumberYearStyle.None ? DigitsField : YearField;
            problems[field] = [$"A number must begin with a letter or a digit other than 0, and {first} does not."];
        }

        return problems;
    }

    private static bool IsAllowed(char character, SerialNumberRule rule) =>
        char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character) || rule.SpecialCharacters.Contains(character, StringComparison.Ordinal);

    private static bool IsLetterOrNonZeroNumeral(char character) => char.IsAsciiLetterUpper(character) || character is >= '1' and <= '9';

    private static string Describe(string specialCharacters) =>
        specialCharacters.Length == 0
            ? "no other character"
            : string.Join(" or ", specialCharacters.Select(character => $"\"{character}\""));
}
