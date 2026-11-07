namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Timeline;

// The days from one date to another, both included, or from a date with no end. A span is covered by others when every one of
// its days lies in one of them, which may follow each other or overlap.
internal readonly record struct DateSpan
{
    public DateSpan(DateOnly from, DateOnly? until)
    {
        if (until < from)
        {
            throw new ArgumentOutOfRangeException(nameof(until), until, $"A span from {from:yyyy-MM-dd} cannot end before it starts.");
        }

        From = from;
        Until = until;
    }

    public DateOnly From { get; }

    public DateOnly? Until { get; }

    public static bool IsCovered(DateSpan span, IEnumerable<DateSpan> by)
    {
        ArgumentNullException.ThrowIfNull(by);

        var spans = by.ToList();
        var day = span.From;
        while (true)
        {
            var containing = spans.Where(candidate => candidate.Contains(day)).ToList();
            if (containing.Count == 0)
            {
                return false;
            }

            if (containing.Any(candidate => candidate.Until is null))
            {
                return true;
            }

            var reach = containing.Max(candidate => candidate.Until!.Value);
            if (span.Until is { } end && reach >= end)
            {
                return true;
            }

            if (reach == DateOnly.MaxValue)
            {
                return false;
            }

            day = reach.AddDays(1);
        }
    }

    public bool Contains(DateOnly day) => From <= day && (Until is not { } until || day <= until);

    public bool Overlaps(DateSpan other) =>
        From <= (other.Until ?? DateOnly.MaxValue) && other.From <= (Until ?? DateOnly.MaxValue);
}
