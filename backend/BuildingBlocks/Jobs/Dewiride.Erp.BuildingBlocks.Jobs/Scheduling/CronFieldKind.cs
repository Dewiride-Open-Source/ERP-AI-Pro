namespace Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;

internal sealed record CronFieldKind(string Name, int Min, int Max)
{
    public static readonly CronFieldKind Minute = new("minute", 0, 59);

    public static readonly CronFieldKind Hour = new("hour", 0, 23);

    public static readonly CronFieldKind DayOfMonth = new("day of month", 1, 31);

    public static readonly CronFieldKind Month = new("month", 1, 12);

    // 0 and 7 are both Sunday, as in crontab(5), so 5-7 is Friday to Sunday.
    public static readonly CronFieldKind DayOfWeek = new("day of week", 0, 7);

    public int Count => Max - Min + 1;
}
