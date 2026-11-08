using System.Globalization;
using System.Text.RegularExpressions;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;

// A column of a set, declared in its seed file beside the data. A value is checked as canonical text: a number without
// trailing zeros and in its column's range, a code, a GST State code or a choice as written, a date as yyyy-MM-dd, and a
// text in the shape every text of a set keeps. A percent is 0 to 100 with at most four decimals and an amount is whole
// rupees, never negative.
internal sealed partial record ParameterColumn
{
    public const int NameMaxLength = 40;

    public const int TitleMaxLength = 60;

    public const int TextMaxLength = 2000;

    public const int CodeMaxLength = 64;

    public const int PercentMaxDecimals = 4;

    public const string DateFormat = "yyyy-MM-dd";

    public ParameterColumn(
        string name,
        string title,
        CellKind kind,
        bool isKey,
        bool isRowTitle,
        bool isOptional,
        decimal? minimum,
        decimal? maximum,
        IEnumerable<ParameterChoice> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);

        if (name is null || !NamePattern().IsMatch(name))
        {
            throw new ArgumentException($"The column name '{name}' is not camelCase: a lower-case letter, then at most {NameMaxLength - 1} letters and digits.", nameof(name));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, $"Column {name} has no kind of the store's.");
        }

        if (isKey && kind is not (CellKind.Code or CellKind.StateCode))
        {
            throw new ArgumentException($"Column {name} is the key, which is of kind code or stateCode, not {kind}.", nameof(isKey));
        }

        if (isKey && isOptional)
        {
            throw new ArgumentException($"Column {name} is the key, which is never optional.", nameof(isOptional));
        }

        if ((minimum is not null || maximum is not null) && !IsNumeric(kind))
        {
            throw new ArgumentException($"Column {name} is of kind {kind}; only an integer, percent or amount column declares a minimum or a maximum.", nameof(minimum));
        }

        if (minimum is { } low && NumberProblem(kind, low) is { } lowProblem)
        {
            throw new ArgumentException($"The minimum of column {name} {lowProblem}.", nameof(minimum));
        }

        if (maximum is { } high && NumberProblem(kind, high) is { } highProblem)
        {
            throw new ArgumentException($"The maximum of column {name} {highProblem}.", nameof(maximum));
        }

        if (minimum > maximum)
        {
            throw new ArgumentException($"The minimum of column {name} exceeds its maximum.", nameof(minimum));
        }

        var declared = choices.ToList();
        CheckChoices(name, kind, declared);

        Name = name;
        Title = ParameterText.Require(title, TitleMaxLength, $"The title of column {name}");
        Kind = kind;
        IsKey = isKey;
        IsRowTitle = isRowTitle;
        IsOptional = isOptional;
        Minimum = minimum;
        Maximum = maximum;
        Choices = declared;
    }

    public string Name { get; }

    public string Title { get; }

    public CellKind Kind { get; }

    public bool IsKey { get; }

    public bool IsRowTitle { get; }

    public bool IsOptional { get; }

    public decimal? Minimum { get; }

    public decimal? Maximum { get; }

    public IReadOnlyList<ParameterChoice> Choices { get; }

    public static bool IsNumeric(CellKind kind) => kind is CellKind.Integer or CellKind.Percent or CellKind.Amount;

    public string? Problem(string? value)
    {
        if (value is null)
        {
            return IsOptional ? null : "it is empty, but the column is not optional";
        }

        return Kind switch
        {
            CellKind.Text => ParameterText.IsWellFormed(value, TextMaxLength)
                ? null
                : $"its text is not 1 to {TextMaxLength} characters without spaces at either end, two spaces in a row or a control character",
            CellKind.Code => CodePattern().IsMatch(value) ? null : $"'{value}' is not a code of 1 to {CodeMaxLength} letters, digits, brackets, dots and hyphens",
            CellKind.StateCode => StateCodePattern().IsMatch(value) ? null : $"'{value}' is not a GST State code of two digits",
            CellKind.Choice => Choices.Any(choice => string.Equals(choice.Value, value, StringComparison.Ordinal)) ? null : $"'{value}' is not one of the column's choices",
            CellKind.Date => IsDate(value) ? null : $"'{value}' is not a date written yyyy-MM-dd",
            _ => NumberValueProblem(value),
        };
    }

    public bool Equals(ParameterColumn? other) =>
        other is not null
        && string.Equals(Name, other.Name, StringComparison.Ordinal)
        && string.Equals(Title, other.Title, StringComparison.Ordinal)
        && Kind == other.Kind
        && IsKey == other.IsKey
        && IsRowTitle == other.IsRowTitle
        && IsOptional == other.IsOptional
        && Minimum == other.Minimum
        && Maximum == other.Maximum
        && Choices.SequenceEqual(other.Choices);

    public override int GetHashCode() => HashCode.Combine(Name, Title, Kind, IsKey, IsRowTitle, IsOptional, Choices.Count);

    private static bool IsDate(string value) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
        && string.Equals(date.ToString(DateFormat, CultureInfo.InvariantCulture), value, StringComparison.Ordinal);

    private static string? NumberProblem(CellKind kind, decimal value) => kind switch
    {
        CellKind.Integer when decimal.Truncate(value) != value || value < int.MinValue || value > int.MaxValue => $"is not a whole number from {int.MinValue} to {int.MaxValue}",
        CellKind.Percent when value is < 0m or > 100m => "is not between 0 and 100",
        CellKind.Percent when CanonicalNumber.DecimalsOf(value) > PercentMaxDecimals => $"has more than {PercentMaxDecimals} decimals",
        CellKind.Amount when decimal.Truncate(value) != value || value < 0m => "is not a whole, non-negative number of rupees",
        _ => null,
    };

    private static void CheckChoices(string name, CellKind kind, List<ParameterChoice> choices)
    {
        if (kind != CellKind.Choice)
        {
            if (choices.Count > 0)
            {
                throw new ArgumentException($"Column {name} is of kind {kind}; only a choice column declares choices.", nameof(choices));
            }

            return;
        }

        if (choices.Count < 2)
        {
            throw new ArgumentException($"Column {name} is a choice column, which declares two or more choices.", nameof(choices));
        }

        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var choice in choices)
        {
            if (choice?.Value is not { } value || !ChoicePattern().IsMatch(value))
            {
                throw new ArgumentException($"A choice of column {name}, '{choice?.Value}', is not camelCase.", nameof(choices));
            }

            if (!values.Add(value))
            {
                throw new ArgumentException($"Column {name} declares the choice '{value}' twice.", nameof(choices));
            }

            ParameterText.Require(choice.Title, TitleMaxLength, $"The title of choice {value} of column {name}");
        }
    }

    private string? NumberValueProblem(string value)
    {
        if (!CanonicalNumber.TryParse(value, out var number))
        {
            return $"'{value}' is not a number written without trailing zeros or an exponent";
        }

        if (NumberProblem(Kind, number) is { } problem)
        {
            return $"{value} {problem}";
        }

        if (number < Minimum)
        {
            return $"{value} is below the minimum {CanonicalNumber.Of(Minimum.Value)}";
        }

        return number > Maximum ? $"{value} is above the maximum {CanonicalNumber.Of(Maximum!.Value)}" : null;
    }

    [GeneratedRegex(@"^[a-z][A-Za-z0-9]{0,39}\z")]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"^[a-z][A-Za-z0-9]*\z")]
    private static partial Regex ChoicePattern();

    [GeneratedRegex(@"^[A-Za-z0-9().\-]{1,64}\z")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"^[0-9]{2}\z")]
    private static partial Regex StateCodePattern();
}
