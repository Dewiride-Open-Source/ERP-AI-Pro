using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Domain;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.UnitTests.IndianStates.Domain;

public sealed class IndianStateTests
{
    private static readonly DateOnly From = new(2017, 7, 1);

    private static readonly DateOnly Until = new(2020, 7, 31);

    [Theory]
    [InlineData("2017-06-30", false)]
    [InlineData("2017-07-01", true)]
    [InlineData("2020-07-31", true)]
    [InlineData("2020-08-01", false)]
    public void AppliesOn_DateAgainstAnEndedCode_IsTrueOnlyWithinItsDatesInclusive(string date, bool expected)
    {
        var state = IndianState.Create("25", From, "Daman and Diu", TerritoryKind.UnionTerritory, Until, "Instrument");

        Assert.Equal(expected, state.AppliesOn(DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void AppliesOn_CodeWithoutAnEnd_HoldsForEveryLaterDate()
    {
        var state = IndianState.Create("27", From, "Maharashtra", TerritoryKind.State, null, "Instrument");

        Assert.True(state.AppliesOn(new DateOnly(9999, 12, 31)));
        Assert.False(state.AppliesOn(From.AddDays(-1)));
    }

    [Theory]
    [InlineData("2")]
    [InlineData("123")]
    [InlineData("AB")]
    [InlineData("")]
    public void Create_CodeThatIsNotTwoDigits_Throws(string code)
    {
        Assert.Throws<ArgumentException>(() => IndianState.Create(code, From, "Name", TerritoryKind.State, null, "Instrument"));
    }

    [Fact]
    public void Revise_EndBeforeTheStart_ThrowsAndKeepsTheRow()
    {
        var state = IndianState.Create("25", From, "Daman and Diu", TerritoryKind.UnionTerritory, Until, "Instrument");

        Assert.Throws<ArgumentOutOfRangeException>(() => state.Revise("Daman and Diu", TerritoryKind.UnionTerritory, From.AddDays(-1), "Instrument"));
        Assert.Equal(Until, state.ValidUntil);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" Goa")]
    [InlineData("Goa ")]
    public void Revise_MalformedName_Throws(string name)
    {
        var state = IndianState.Create("30", From, "Goa", TerritoryKind.State, null, "Instrument");

        Assert.Throws<ArgumentException>(() => state.Revise(name, TerritoryKind.State, null, "Instrument"));
    }

    [Fact]
    public void Revise_UndefinedKindOrBlankSource_Throws()
    {
        var state = IndianState.Create("30", From, "Goa", TerritoryKind.State, null, "Instrument");

        Assert.Throws<ArgumentOutOfRangeException>(() => state.Revise("Goa", (TerritoryKind)9, null, "Instrument"));
        Assert.Throws<ArgumentException>(() => state.Revise("Goa", TerritoryKind.State, null, " "));
    }
}
