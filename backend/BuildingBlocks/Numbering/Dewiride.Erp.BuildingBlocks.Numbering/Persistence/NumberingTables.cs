namespace Dewiride.Erp.BuildingBlocks.Numbering.Persistence;

public static class NumberingTables
{
    public const string Schema = "platform_numbering";

    public const string Series = "NumberSeries";

    public const string Years = "NumberSeriesYears";

    public const string SeriesChangeKey = "platform.numbering.series";

    public const string YearsChangeKey = "platform.numbering.series-years";

    public const int YearLength = 7;
}
