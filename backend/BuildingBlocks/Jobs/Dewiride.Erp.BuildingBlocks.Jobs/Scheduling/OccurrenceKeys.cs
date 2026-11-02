using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;

// The key of a scheduled occurrence names its minute in India Standard Time, the time the schedule speaks in, so the run of the
// night of 2 November reads occurrence:2026-11-02T00:30+05:30 and no other enqueue can take it.
internal static class OccurrenceKeys
{
    public const string Prefix = "occurrence:";

    public static string For(DateTimeOffset occurrence) =>
        Prefix + IndianStandardTime.ToIst(occurrence).ToString("yyyy-MM-dd'T'HH:mmzzz", CultureInfo.InvariantCulture);
}
