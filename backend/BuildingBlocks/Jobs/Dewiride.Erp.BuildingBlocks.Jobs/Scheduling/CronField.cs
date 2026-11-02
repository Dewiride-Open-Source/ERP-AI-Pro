using System.Globalization;
using System.Numerics;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;

// One field of a five-field schedule, held as a bit set of its values. Only numbers are accepted, never names, so every value has
// one spelling. A field is unrestricted when its text begins with *, as crontab(5) of Ubuntu's cron package defines it, so */2
// filters the values yet counts as unrestricted when the two day fields combine.
internal sealed class CronField
{
    private const int SundayAsSeven = 7;

    private readonly ulong _values;

    private CronField(ulong values, bool unrestricted)
    {
        _values = values;
        Unrestricted = unrestricted;
    }

    public bool Unrestricted { get; }

    public IEnumerable<int> Values
    {
        get
        {
            for (var value = 0; value < 64; value++)
            {
                if (Contains(value))
                {
                    yield return value;
                }
            }
        }
    }

    public bool Contains(int value) => (uint)value < 64 && (_values & (1UL << value)) != 0;

    public int? FirstAtOrAfter(int value)
    {
        if ((uint)value >= 64)
        {
            return null;
        }

        var remaining = _values & (ulong.MaxValue << value);

        return remaining == 0 ? null : BitOperations.TrailingZeroCount(remaining);
    }

    public static Result<CronField> Parse(string text, CronFieldKind kind)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        ArgumentNullException.ThrowIfNull(kind);

        ulong values = 0;
        if (text == "*")
        {
            values = Span(kind.Min, kind.Max, 1);
        }
        else
        {
            foreach (var item in text.Split(','))
            {
                var parsed = ParseItem(item, kind);
                if (parsed.IsFailure)
                {
                    return parsed.Error!;
                }

                values |= parsed.Value;
            }
        }

        if (kind == CronFieldKind.DayOfWeek && (values & (1UL << SundayAsSeven)) != 0)
        {
            values = (values & ~(1UL << SundayAsSeven)) | 1UL;
        }

        return new CronField(values, text[0] == '*');
    }

    private static Result<ulong> ParseItem(string item, CronFieldKind kind)
    {
        if (item.Length == 0)
        {
            return Invalid($"The {kind.Name} field has an empty item in its list.");
        }

        if (item == "*")
        {
            return Invalid($"The {kind.Name} field lists * among other values; * stands alone.");
        }

        var slash = item.IndexOf('/', StringComparison.Ordinal);
        var range = slash < 0 ? item : item[..slash];
        var stepText = slash < 0 ? null : item[(slash + 1)..];
        int low;
        int high;
        if (range == "*")
        {
            low = kind.Min;
            high = kind.Max;
        }
        else
        {
            var dash = range.IndexOf('-', StringComparison.Ordinal);
            if (dash < 0)
            {
                if (!TryNumber(range, out low))
                {
                    return NotAToken(item, kind);
                }

                if (stepText is not null)
                {
                    return Invalid($"The {kind.Name} field holds '{item}', but a step follows * or a range, such as */15 or 0-30/5.");
                }

                high = low;
            }
            else if (!TryNumber(range[..dash], out low) || !TryNumber(range[(dash + 1)..], out high))
            {
                return NotAToken(item, kind);
            }
        }

        var step = 1;
        if (stepText is not null && !TryNumber(stepText, out step))
        {
            return NotAToken(item, kind);
        }

        foreach (var value in (int[])[low, high])
        {
            if (value < kind.Min || value > kind.Max)
            {
                return Invalid($"The {kind.Name} field holds {value}, but the {kind.Name} allows {kind.Min} to {kind.Max}.");
            }
        }

        if (low > high)
        {
            return Invalid($"The {kind.Name} field holds the range {low}-{high}, which starts after it ends.");
        }

        if (step < 1 || step > kind.Count)
        {
            return Invalid($"The {kind.Name} field holds the step {step}, but a step of the {kind.Name} is 1 to {kind.Count}.");
        }

        return Span(low, high, step);
    }

    private static ulong Span(int low, int high, int step)
    {
        ulong values = 0;
        for (var value = low; value <= high; value += step)
        {
            values |= 1UL << value;
        }

        return values;
    }

    private static bool TryNumber(string text, out int value)
    {
        value = 0;

        return text.Length is 1 or 2
            && text.All(char.IsAsciiDigit)
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static Error NotAToken(string item, CronFieldKind kind) =>
        Invalid($"The {kind.Name} field holds '{item}', which is not a number, a range, a step or *. Months and days of the week are numbers: 1 to 12, and 0 to 7 with 0 and 7 for Sunday.");

    private static Error Invalid(string message) => Error.Validation(CronSchedule.InvalidCode, message);
}
