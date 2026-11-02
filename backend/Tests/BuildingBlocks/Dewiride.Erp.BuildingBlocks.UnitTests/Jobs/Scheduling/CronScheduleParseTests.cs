using Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Jobs.Scheduling;

public sealed class CronScheduleParseTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("* * * *")]
    [InlineData("* * * * * *")]
    [InlineData("60 * * * *")]
    [InlineData("* 24 * * *")]
    [InlineData("* * 0 * *")]
    [InlineData("* * 32 * *")]
    [InlineData("* * * 0 *")]
    [InlineData("* * * 13 *")]
    [InlineData("* * * * 8")]
    [InlineData("5-1 * * * *")]
    [InlineData("*/0 * * * *")]
    [InlineData("*/61 * * * *")]
    [InlineData("5/15 * * * *")]
    [InlineData("1,,2 * * * *")]
    [InlineData("1, * * * *")]
    [InlineData("*,5 * * * *")]
    [InlineData("* * * JAN *")]
    [InlineData("* * * * MON")]
    [InlineData("0 0 30 2 *")]
    [InlineData("0 0 31 4,6,9,11 *")]
    [InlineData("0 0 31 2 */7")]
    [InlineData("a * * * *")]
    [InlineData("-1 * * * *")]
    [InlineData("1- * * * *")]
    [InlineData("*/ * * * *")]
    [InlineData("０ * * * *")]
    [InlineData("30\t0 * * * *")]
    [InlineData("001 * * * *")]
    [InlineData("1-2-3 * * * *")]
    [InlineData("0-30/5/2 * * * *")]
    public void Parse_MalformedExpression_IsRefusedAsAnInvalidSchedule(string expression)
    {
        var parsed = CronSchedule.Parse(expression);

        Assert.True(parsed.IsFailure);
        Assert.Equal(CronSchedule.InvalidCode, parsed.Error!.Code);
        Assert.Equal(ErrorKind.Validation, parsed.Error.Kind);
    }

    [Theory]
    [InlineData(null, "The schedule is empty: write five fields")]
    [InlineData("* * * *", "The schedule has 4 fields; it needs five")]
    [InlineData("60 * * * *", "The minute field holds 60, but the minute allows 0 to 59.")]
    [InlineData("* * * * 8", "The day of week field holds 8, but the day of week allows 0 to 7.")]
    [InlineData("5-1 * * * *", "The minute field holds the range 5-1, which starts after it ends.")]
    [InlineData("*/0 * * * *", "The minute field holds the step 0, but a step of the minute is 1 to 60.")]
    [InlineData("* */25 * * *", "The hour field holds the step 25, but a step of the hour is 1 to 24.")]
    [InlineData("5/15 * * * *", "The minute field holds '5/15', but a step follows * or a range, such as */15 or 0-30/5.")]
    [InlineData("1,,2 * * * *", "The minute field has an empty item in its list.")]
    [InlineData("*,5 * * * *", "The minute field lists * among other values; * stands alone.")]
    [InlineData("* * * JAN *", "The month field holds 'JAN', which is not a number, a range, a step or *. Months and days of the week are numbers")]
    [InlineData("0 0 30 2 *", "The schedule never runs: no month it names has day 30.")]
    [InlineData("0 0 30,31 2 *", "The schedule never runs: no month it names has day 30 or 31.")]
    public void Parse_MalformedExpression_SaysWhatIsWrong(string? expression, string message)
    {
        var parsed = CronSchedule.Parse(expression);

        Assert.StartsWith(message, parsed.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_LongerThanTheLimit_IsRefused()
    {
        var parsed = CronSchedule.Parse("0 0 * * " + string.Join(',', Enumerable.Repeat("1", 100)));

        Assert.Equal($"The schedule is longer than {CronSchedule.MaxLength} characters.", parsed.Error!.Message);
    }

    [Theory]
    [InlineData(" 30  0 * * * ", "30 0 * * *")]
    [InlineData("00 05 * * *", "00 05 * * *")]
    [InlineData("*/15,7 * * * *", "*/15,7 * * * *")]
    [InlineData("0 0 * * 1-7", "0 0 * * 1-7")]
    [InlineData("0 0 29 2 *", "0 0 29 2 *")]
    [InlineData("0 0 30 2 1", "0 0 30 2 1")]
    [InlineData("59 23 31 12 0-7/7", "59 23 31 12 0-7/7")]
    public void Parse_WellFormedExpression_KeepsItsTokensWithTheSpacesCollapsed(string expression, string normalised)
    {
        var parsed = CronSchedule.Parse(expression);

        Assert.True(parsed.IsSuccess, parsed.Error?.Message);
        Assert.Equal(normalised, parsed.Value.Expression);
    }
}
