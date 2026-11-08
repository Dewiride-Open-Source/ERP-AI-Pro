using System.Globalization;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Application.Queries.GetParameterSet;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.UnitTests.ParameterSets.Application;

public sealed class StateNamesTests
{
    private static readonly StateNames Names = new([
        new IndianStateEntry("26", "Dadra and Nagar Haveli", IndianStateKind.UnionTerritory, new DateOnly(2017, 7, 1), new DateOnly(2020, 1, 25)),
        new IndianStateEntry("26", "Dadra and Nagar Haveli and Daman and Diu", IndianStateKind.UnionTerritory, new DateOnly(2020, 1, 26), null),
        new IndianStateEntry("27", "Maharashtra", IndianStateKind.State, new DateOnly(2017, 7, 1), null)]);

    [Theory]
    [InlineData("26", "2020-01-25", "Dadra and Nagar Haveli")]
    [InlineData("26", "2020-01-26", "Dadra and Nagar Haveli and Daman and Diu")]
    [InlineData("27", "2017-07-01", "Maharashtra")]
    [InlineData("27", "2026-04-01", "Maharashtra")]
    public void NameOf_CodeInForceOnTheDay_IsTheNameOfThatDay(string code, string day, string name)
    {
        Assert.Equal(name, Names.NameOf(code, DateOnly.Parse(day, CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData("27", "2017-06-30")]
    [InlineData("99", "2026-04-01")]
    public void NameOf_CodeNotInForceOnTheDay_IsNull(string code, string day)
    {
        Assert.Null(Names.NameOf(code, DateOnly.Parse(day, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void None_NamesNoState()
    {
        Assert.Null(StateNames.None.NameOf("27", new DateOnly(2026, 4, 1)));
    }
}
