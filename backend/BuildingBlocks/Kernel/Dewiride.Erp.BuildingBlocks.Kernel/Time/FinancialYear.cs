using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Time;

[JsonConverter(typeof(FinancialYearJsonConverter))]
public readonly record struct FinancialYear : IComparable<FinancialYear>, IParsable<FinancialYear>
{
    public const int StartMonth = 4;

    public const int StartDay = 1;

    public FinancialYear(int startYear)
    {
        if (startYear is < 1 or > 9998)
        {
            throw new ArgumentOutOfRangeException(nameof(startYear), startYear, "A financial year starts in a calendar year between 1 and 9998.");
        }

        StartYear = startYear;
    }

    public int StartYear { get; }

    public DateOnly Start => new(StartYear, StartMonth, StartDay);

    public DateOnly End => Start.AddYears(1).AddDays(-1);

    public string Label => Format(StartYear);

    public string AssessmentYearLabel => Format(StartYear + 1);

    public FinancialYear Next => new(StartYear + 1);

    public FinancialYear Previous => new(StartYear - 1);

    public IReadOnlyList<FinancialQuarter> Quarters => [new(this, 1), new(this, 2), new(this, 3), new(this, 4)];

    public static FinancialYear FromDate(DateOnly date) => new(date.Month >= StartMonth ? date.Year : date.Year - 1);

    public static FinancialYear Current(TimeProvider timeProvider) => FromDate(IndianStandardTime.Today(timeProvider));

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    public FinancialQuarter QuarterOf(DateOnly date) =>
        Contains(date)
            ? new FinancialQuarter(this, ((date.Month - StartMonth + 12) % 12 / 3) + 1)
            : throw new ArgumentOutOfRangeException(nameof(date), date, string.Create(CultureInfo.InvariantCulture, $"{date:yyyy-MM-dd} is outside financial year {Label}."));

    public int CompareTo(FinancialYear other) => StartYear.CompareTo(other.StartYear);

    public static bool operator <(FinancialYear left, FinancialYear right) => left.StartYear < right.StartYear;

    public static bool operator >(FinancialYear left, FinancialYear right) => left.StartYear > right.StartYear;

    public static bool operator <=(FinancialYear left, FinancialYear right) => left.StartYear <= right.StartYear;

    public static bool operator >=(FinancialYear left, FinancialYear right) => left.StartYear >= right.StartYear;

    public static FinancialYear Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out var year) ? year : throw new FormatException($"'{s}' is not a financial year label of the form '2026-27'.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out FinancialYear result)
    {
        result = default;
        if (s is null || s.Length != 7 || s[4] != '-'
            || !int.TryParse(s.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var startYear)
            || !int.TryParse(s.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var endSuffix)
            || startYear is < 1 or > 9998
            || (startYear + 1) % 100 != endSuffix)
        {
            return false;
        }

        result = new FinancialYear(startYear);
        return true;
    }

    public override string ToString() => Label;

    private static string Format(int startYear) => string.Create(CultureInfo.InvariantCulture, $"{startYear:0000}-{(startYear + 1) % 100:00}");
}
