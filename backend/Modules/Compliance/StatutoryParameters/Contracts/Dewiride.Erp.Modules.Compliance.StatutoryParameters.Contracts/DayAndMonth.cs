using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;

// A day of the year without its year, such as the 1 April that opens a contribution period, written MM-dd as the seed files
// and the API write it. 29 February is a day of leap years only, so a consumer places it in a year before it counts with it.
[JsonConverter(typeof(DayAndMonthJsonConverter))]
public readonly record struct DayAndMonth
{
    private const int LeapYear = 2000;

    public DayAndMonth(int month, int day)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), month, "A month is 1 to 12.");
        }

        if (day < 1 || day > DateTime.DaysInMonth(LeapYear, month))
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, $"Month {month} has no day {day}.");
        }

        Month = month;
        Day = day;
    }

    public int Month { get; }

    public int Day { get; }

    public static bool TryParse([NotNullWhen(true)] string? text, out DayAndMonth result)
    {
        result = default;
        if (text is not { Length: 5 }
            || text[2] != '-'
            || !int.TryParse(text.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || !int.TryParse(text.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var day)
            || month is < 1 or > 12
            || day < 1
            || day > DateTime.DaysInMonth(LeapYear, month))
        {
            return false;
        }

        result = new DayAndMonth(month, day);
        return true;
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Month:00}-{Day:00}");
}
