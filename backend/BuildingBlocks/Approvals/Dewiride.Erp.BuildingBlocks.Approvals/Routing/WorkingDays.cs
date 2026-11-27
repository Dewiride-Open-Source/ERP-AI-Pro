using Dewiride.Erp.BuildingBlocks.Kernel.Time;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Routing;

// A step that starts at an instant is due by the end of the Nth working day after that instant's date in India, so the day it
// starts on never counts against it. Working days are Monday to Friday; a public holiday counts as a working day until the ERP
// holds a holiday calendar. The due time is the first instant past that day, midnight in India, so a step is late from it on.
internal static class WorkingDays
{
    public static DateTimeOffset DueAt(DateTimeOffset startedAt, int workingDays)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(workingDays, 1);

        var day = IndianStandardTime.IstDate(startedAt);
        for (var counted = 0; counted < workingDays;)
        {
            day = day.AddDays(1);
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                counted++;
            }
        }

        return IndianStandardTime.StartOfIstDay(day.AddDays(1)).ToUniversalTime();
    }
}
