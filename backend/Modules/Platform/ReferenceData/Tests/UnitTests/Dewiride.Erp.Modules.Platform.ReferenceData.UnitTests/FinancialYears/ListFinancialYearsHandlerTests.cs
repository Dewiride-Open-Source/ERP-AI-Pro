using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Platform.ReferenceData.FinancialYears.Application.Queries.ListFinancialYears;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.UnitTests.FinancialYears;

public sealed class ListFinancialYearsHandlerTests
{
    private readonly ListFinancialYearsHandler _handler = new();

    [Fact]
    public async Task Handle_Range_ListsEveryYearFromTheFirstToTheLastInclusive()
    {
        var result = await _handler.HandleAsync(new ListFinancialYearsQuery("2024-25", "2027-28"), TestContext.Current.CancellationToken);

        Assert.Equal(["2024-25", "2025-26", "2026-27", "2027-28"], result.Value.Select(year => year.Label));
    }

    [Fact]
    public async Task Handle_OneYear_ListsThatYear()
    {
        var result = await _handler.HandleAsync(new ListFinancialYearsQuery("2026-27", "2026-27"), TestContext.Current.CancellationToken);

        Assert.Equal(new FinancialYear(2026), Assert.Single(result.Value));
    }

    [Fact]
    public async Task Handle_FiftyYears_ListsThemAndFiftyOneFailKeyedByTo()
    {
        var fifty = await _handler.HandleAsync(new ListFinancialYearsQuery("2000-01", "2049-50"), TestContext.Current.CancellationToken);
        var fiftyOne = await _handler.HandleAsync(new ListFinancialYearsQuery("2000-01", "2050-51"), TestContext.Current.CancellationToken);

        Assert.Equal(50, fifty.Value.Count);
        Assert.Equal("financial-year.range-too-long", fiftyOne.Error!.Code);
        Assert.Equal("to", Assert.Single(fiftyOne.Error.Fields.Keys));
    }

    [Fact]
    public async Task Handle_LastBeforeFirst_FailsKeyedByTo()
    {
        var result = await _handler.HandleAsync(new ListFinancialYearsQuery("2026-27", "2025-26"), TestContext.Current.CancellationToken);

        Assert.Equal("financial-year.range-reversed", result.Error!.Code);
        Assert.Equal("to", Assert.Single(result.Error.Fields.Keys));
    }

    [Theory]
    [InlineData("2026-28", "2026-27", "from")]
    [InlineData("2026-27", "2027-29", "to")]
    [InlineData("0000-01", "2026-27", "from")]
    public async Task Handle_LabelThatIsNoYear_FailsKeyedByItsParameter(string from, string to, string member)
    {
        var result = await _handler.HandleAsync(new ListFinancialYearsQuery(from, to), TestContext.Current.CancellationToken);

        Assert.Equal("financial-year.label-invalid", result.Error!.Code);
        Assert.Equal(member, Assert.Single(result.Error.Fields.Keys));
    }

    [Fact]
    public async Task Handle_LastYearTheKernelKnows_ListsItWithoutGoingPastIt()
    {
        var result = await _handler.HandleAsync(new ListFinancialYearsQuery("9997-98", "9998-99"), TestContext.Current.CancellationToken);

        Assert.Equal([new FinancialYear(9997), new FinancialYear(9998)], result.Value);
    }
}
