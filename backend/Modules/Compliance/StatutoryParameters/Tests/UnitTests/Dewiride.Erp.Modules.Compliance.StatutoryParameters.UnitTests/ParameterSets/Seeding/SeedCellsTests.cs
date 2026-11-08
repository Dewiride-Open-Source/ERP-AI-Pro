using System.Text.Json;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.UnitTests.ParameterSets.Seeding;

public sealed class SeedCellsTests
{
    [Theory]
    [InlineData(nameof(CellKind.Percent), "2.50", "2.5")]
    [InlineData(nameof(CellKind.Percent), "9", "9")]
    [InlineData(nameof(CellKind.Percent), "9.0000", "9")]
    [InlineData(nameof(CellKind.Percent), "0.0001", "0.0001")]
    [InlineData(nameof(CellKind.Integer), "11", "11")]
    [InlineData(nameof(CellKind.Integer), "11.0", "11")]
    [InlineData(nameof(CellKind.Amount), "50000000", "50000000")]
    [InlineData(nameof(CellKind.Amount), "5000000.00", "5000000")]
    [InlineData(nameof(CellKind.Amount), "0.75", "0.75")]
    [InlineData(nameof(CellKind.Amount), "2.50", "2.5")]
    [InlineData(nameof(CellKind.Text), "\"Selling of space for advertisement in print media.\"", "Selling of space for advertisement in print media.")]
    [InlineData(nameof(CellKind.Code), "\"21(ia)\"", "21(ia)")]
    [InlineData(nameof(CellKind.StateCode), "\"07\"", "07")]
    [InlineData(nameof(CellKind.Date), "\"2019-10-01\"", "2019-10-01")]
    [InlineData(nameof(CellKind.DayMonth), "\"04-01\"", "04-01")]
    public void Read_ValueOfItsKind_KeepsItsCanonicalText(string kindName, string json, string expected)
    {
        var kind = Enum.Parse<CellKind>(kindName);

        Assert.Equal(expected, SeedCells.Read(TestSets.Column("value", kind), Json(json)));
    }

    [Fact]
    public void Read_Choice_KeepsTheDeclaredValue()
    {
        var column = TestSets.Column("filers", CellKind.Choice, choices: [new("monthly", "Monthly filers"), new("quarterly", "Quarterly filers")]);

        Assert.Equal("quarterly", SeedCells.Read(column, Json("\"quarterly\"")));
        Assert.Throws<ArgumentException>(() => SeedCells.Read(column, Json("\"Quarterly\"")));
    }

    [Theory]
    [InlineData(nameof(CellKind.Percent), "\"9\"")]
    [InlineData(nameof(CellKind.Integer), "\"11\"")]
    [InlineData(nameof(CellKind.Amount), "\"50000000\"")]
    [InlineData(nameof(CellKind.Code), "21")]
    [InlineData(nameof(CellKind.StateCode), "27")]
    [InlineData(nameof(CellKind.Text), "true")]
    [InlineData(nameof(CellKind.Text), "[\"a\"]")]
    [InlineData(nameof(CellKind.Date), "20191001")]
    [InlineData(nameof(CellKind.DayMonth), "401")]
    public void Read_ValueOfTheWrongJsonType_IsRefused(string kindName, string json)
    {
        var kind = Enum.Parse<CellKind>(kindName);

        Assert.Throws<ArgumentException>(() => SeedCells.Read(TestSets.Column("value", kind), Json(json)));
    }

    [Theory]
    [InlineData(nameof(CellKind.Integer), "11.5")]
    [InlineData(nameof(CellKind.Percent), "100.00001")]
    [InlineData(nameof(CellKind.Percent), "100.0001")]
    [InlineData(nameof(CellKind.Percent), "0.12345")]
    [InlineData(nameof(CellKind.Percent), "-0.5")]
    [InlineData(nameof(CellKind.Amount), "0.755")]
    [InlineData(nameof(CellKind.Amount), "-100")]
    [InlineData(nameof(CellKind.Amount), "1e30")]
    [InlineData(nameof(CellKind.Text), "\"two  spaces\"")]
    [InlineData(nameof(CellKind.Code), "\"21 (ii)\"")]
    [InlineData(nameof(CellKind.StateCode), "\"7\"")]
    [InlineData(nameof(CellKind.Date), "\"2019-10-1\"")]
    [InlineData(nameof(CellKind.DayMonth), "\"4-1\"")]
    public void Read_ValueThatBreaksItsKind_IsRefused(string kindName, string json)
    {
        var kind = Enum.Parse<CellKind>(kindName);

        Assert.Throws<ArgumentException>(() => SeedCells.Read(TestSets.Column("value", kind), Json(json)));
    }

    [Fact]
    public void Read_Null_IsKeptOnlyForAnOptionalColumn()
    {
        Assert.Null(SeedCells.Read(TestSets.Column("note", CellKind.Text, isOptional: true), Json("null")));
        Assert.Throws<ArgumentException>(() => SeedCells.Read(TestSets.Column("note", CellKind.Text), Json("null")));
    }

    [Fact]
    public void Read_ValueOutsideTheColumnsBounds_IsRefused()
    {
        var column = TestSets.Column("monthsAfter", CellKind.Integer, minimum: 1, maximum: 12);

        Assert.Equal("12", SeedCells.Read(column, Json("12")));
        Assert.Throws<ArgumentException>(() => SeedCells.Read(column, Json("13")));
        Assert.Throws<ArgumentException>(() => SeedCells.Read(column, Json("0")));
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }
}
