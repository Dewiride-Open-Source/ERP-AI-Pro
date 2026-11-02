using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;

// Five fields of numbers in India Standard Time, a fixed +05:30 with no daylight saving. The day rule is crontab(5)'s: the month,
// hour and minute must match, and when both day fields are restricted a day matches when either does, otherwise when both do,
// an unrestricted * matching every day. So 0 0 */2 * 1 runs on odd-numbered Mondays and 0 0 1-31/2 * 1 on odd days and on
// Mondays. The search ends after one Gregorian cycle of 400 years, within which a day that ever matches recurs, so a schedule
// that passes the check of its day fields always has a next occurrence.
internal sealed class CronSchedule
{
    public const string InvalidCode = "job.schedule-invalid";

    public const int MaxLength = JobSchedule.ExpressionMaxLength;

    private const int FieldCount = 5;

    private const int SearchYears = 400;

    private static readonly int[] LongestMonths = [31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

    private readonly CronField _minutes;

    private readonly CronField _hours;

    private readonly CronField _days;

    private readonly CronField _months;

    private readonly CronField _weekdays;

    private CronSchedule(string expression, CronField minutes, CronField hours, CronField days, CronField months, CronField weekdays)
    {
        Expression = expression;
        _minutes = minutes;
        _hours = hours;
        _days = days;
        _months = months;
        _weekdays = weekdays;
    }

    public string Expression { get; }

    public static Result<CronSchedule> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Invalid("The schedule is empty: write five fields, minute, hour, day of month, month and day of week, such as 30 0 * * *.");
        }

        if (text.Length > MaxLength)
        {
            return Invalid($"The schedule is longer than {MaxLength} characters.");
        }

        var texts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (texts.Length != FieldCount)
        {
            return Invalid($"The schedule has {texts.Length} fields; it needs five: minute, hour, day of month, month and day of week.");
        }

        CronFieldKind[] kinds = [CronFieldKind.Minute, CronFieldKind.Hour, CronFieldKind.DayOfMonth, CronFieldKind.Month, CronFieldKind.DayOfWeek];
        var fields = new CronField[FieldCount];
        for (var index = 0; index < FieldCount; index++)
        {
            var field = CronField.Parse(texts[index], kinds[index]);
            if (field.IsFailure)
            {
                return field.Error!;
            }

            fields[index] = field.Value;
        }

        var (days, months, weekdays) = (fields[2], fields[3], fields[4]);
        if (weekdays.Unrestricted && !days.Unrestricted && !months.Values.Any(month => days.Values.Any(day => day <= LongestMonths[month - 1])))
        {
            return Invalid($"The schedule never runs: no month it names has day {Either(days.Values)}.");
        }

        return new CronSchedule(string.Join(' ', texts), fields[0], fields[1], days, months, weekdays);
    }

    public DateTimeOffset NextAfter(DateTimeOffset instant)
    {
        var ist = IndianStandardTime.ToIst(instant);
        var start = new DateTime(ist.Year, ist.Month, ist.Day, ist.Hour, ist.Minute, 0, DateTimeKind.Unspecified).AddMinutes(1);
        var lastYear = Math.Min(start.Year + SearchYears, DateTime.MaxValue.Year - 1);
        for (var time = start; time.Year <= lastYear;)
        {
            if (!_months.Contains(time.Month))
            {
                time = new DateTime(time.Year, time.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(1);
                continue;
            }

            if (!IsRunDay(time))
            {
                time = time.Date.AddDays(1);
                continue;
            }

            if (_hours.FirstAtOrAfter(time.Hour) is not { } hour)
            {
                time = time.Date.AddDays(1);
                continue;
            }

            if (hour != time.Hour)
            {
                time = time.Date.AddHours(hour);
            }

            if (_minutes.FirstAtOrAfter(time.Minute) is not { } minute)
            {
                time = time.Date.AddHours(time.Hour + 1);
                continue;
            }

            return new DateTimeOffset(time.Date.AddHours(time.Hour).AddMinutes(minute), IndianStandardTime.Offset).ToUniversalTime();
        }

        throw new InvalidOperationException($"The schedule '{Expression}' has no occurrence in the {SearchYears} years after {instant:O}.");
    }

    private bool IsRunDay(DateTime day)
    {
        var dayOfMonth = _days.Contains(day.Day);
        var dayOfWeek = _weekdays.Contains((int)day.DayOfWeek);

        return _days.Unrestricted || _weekdays.Unrestricted ? dayOfMonth && dayOfWeek : dayOfMonth || dayOfWeek;
    }

    private static string Either(IEnumerable<int> values)
    {
        var all = values.Select(value => value.ToString(CultureInfo.InvariantCulture)).ToArray();

        return all.Length == 1 ? all[0] : $"{string.Join(", ", all[..^1])} or {all[^1]}";
    }

    private static Error Invalid(string message) => Error.Validation(InvalidCode, message);
}
