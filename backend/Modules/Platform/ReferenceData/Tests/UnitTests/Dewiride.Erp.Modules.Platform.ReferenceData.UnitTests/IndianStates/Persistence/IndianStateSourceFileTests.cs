using System.Globalization;
using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Persistence;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.UnitTests.IndianStates.Persistence;

public sealed class IndianStateSourceFileTests
{
    private static readonly IndianStateSourceFile File = IndianStateSourceFile.Read();

    [Fact]
    public void Read_SeedFile_HoldsTheTwentyEightStatesEightUnionTerritoriesAndTheOtherTerritoryToday()
    {
        var current = File.States.Where(state => state.ValidUntil is null).ToList();

        Assert.Equal(28, current.Count(state => state.Kind == TerritoryKind.State));
        Assert.Equal(8, current.Count(state => state.Kind == TerritoryKind.UnionTerritory));
        Assert.Equal("97", Assert.Single(current, state => state.Kind == TerritoryKind.OtherTerritory).GstCode);
        Assert.Equal(current.Count, current.Select(state => state.GstCode).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Read_SeedFile_HasNoCode28AndEveryRowNamesItsInstrument()
    {
        Assert.DoesNotContain(File.States, state => state.GstCode == "28");
        Assert.All(File.States, state => Assert.False(string.IsNullOrWhiteSpace(state.Source)));
        Assert.All(File.States, state => Assert.True(state.ValidFrom >= new DateOnly(2017, 7, 1)));
    }

    [Theory]
    [InlineData("25", "2020-07-31", "Daman and Diu")]
    [InlineData("26", "2020-01-25", "Dadra and Nagar Haveli")]
    [InlineData("26", "2020-01-26", "Dadra and Nagar Haveli and Daman and Diu")]
    [InlineData("38", "2020-01-01", "Ladakh")]
    [InlineData("01", "2019-10-30", "Jammu and Kashmir")]
    [InlineData("37", "2017-07-01", "Andhra Pradesh")]
    [InlineData("36", "2026-11-07", "Telangana")]
    public void Read_SeedFile_NamesTheTerritoryOfTheCodeOnTheDate(string code, string date, string name)
    {
        Assert.Equal(name, Assert.Single(RowsOn(code, date)).Name);
    }

    [Theory]
    [InlineData("25", "2020-08-01")]
    [InlineData("38", "2019-12-31")]
    [InlineData("01", "2017-07-07")]
    public void Read_SeedFile_HasNoTerritoryForTheCodeOnTheDate(string code, string date)
    {
        Assert.Empty(RowsOn(code, date));
    }

    [Fact]
    public void Read_SeedFile_MakesJammuAndKashmirAUnionTerritoryFromTheThirtyFirstOfOctober2019()
    {
        Assert.Equal(TerritoryKind.State, Assert.Single(RowsOn("01", "2019-10-30")).Kind);
        Assert.Equal(TerritoryKind.UnionTerritory, Assert.Single(RowsOn("01", "2019-10-31")).Kind);
    }

    private static List<IndianStateSourceEntry> RowsOn(string code, string date)
    {
        var day = DateOnly.Parse(date, CultureInfo.InvariantCulture);

        return [.. File.States.Where(state => state.GstCode == code && state.ValidFrom <= day && (state.ValidUntil is null || state.ValidUntil >= day))];
    }
}
