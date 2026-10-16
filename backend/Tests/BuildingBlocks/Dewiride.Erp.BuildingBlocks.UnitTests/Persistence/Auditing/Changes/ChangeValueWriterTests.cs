using System.Globalization;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes.Capture;
using static Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.Auditing.Changes.CaptureContext;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.Auditing.Changes;

public sealed class ChangeValueWriterTests
{
    [Fact]
    public void Write_NoValues_IsNothing()
    {
        Assert.Null(ChangeValueWriter.Write([]));
    }

    [Theory]
    [InlineData("10.0000", "10")]
    [InlineData("12.50", "12.5")]
    [InlineData("0.0001", "0.0001")]
    [InlineData("-3.1400", "-3.14")]
    [InlineData("0.0000", "0")]
    [InlineData("79228162514264337593543950335", "79228162514264337593543950335")]
    [InlineData("0.0000000000000000000000000001", "0.0000000000000000000000000001")]
    public void Write_Decimal_IsItsShortestExactTextInAString(string amount, string written)
    {
        var value = Single(ChangedValue.Added(Property("price.amount"), decimal.Parse(amount, CultureInfo.InvariantCulture)));

        Assert.Equal(JsonValueKind.String, value.GetProperty("after").ValueKind);
        Assert.Equal(written, value.GetProperty("after").GetString());
    }

    [Fact]
    public void Write_EnumValueThatNamesNoMember_IsItsNumber()
    {
        var value = Single(ChangedValue.Changed(Property("colour"), Colour.Red, (Colour)7));

        Assert.Equal("red", value.GetProperty("before").GetString());
        Assert.Equal("7", value.GetProperty("after").GetString());
    }

    [Fact]
    public void Write_InstantWithAnOffset_IsItsUtcTime()
    {
        var value = Single(ChangedValue.Added(Property("inspectedAt"), new DateTimeOffset(2026, 4, 1, 0, 15, 0, TimeSpan.FromHours(5.5))));

        Assert.Equal("2026-03-31T18:45:00.0000000+00:00", value.GetProperty("after").GetString());
    }

    [Fact]
    public void Write_StronglyTypedId_IsItsGuid()
    {
        var category = CategoryId.Create();

        var value = Single(ChangedValue.Changed(Property("categoryId"), null, category));

        Assert.Equal(JsonValueKind.Null, value.GetProperty("before").ValueKind);
        Assert.Equal(category.Value.ToString("D"), value.GetProperty("after").GetString());
        Assert.Equal(CategoryKey, value.GetProperty("references").GetString());
    }

    [Fact]
    public void Write_ValueObjectWithAConverter_IsWhatItStores()
    {
        var value = Single(ChangedValue.Changed(Property("price.currency"), Currency.Inr, Currency.Usd));

        Assert.Equal("INR", value.GetProperty("before").GetString());
        Assert.Equal("USD", value.GetProperty("after").GetString());
    }

    [Fact]
    public void Write_TextOutsideAscii_ReadsBackUnchanged()
    {
        var value = Single(ChangedValue.Added(Property("name"), "राम <कुमार> & \"सिंह\""));

        Assert.Equal("राम <कुमार> & \"सिंह\"", value.GetProperty("after").GetString());
    }

    [Fact]
    public void Write_CreatedAndRemovedValues_CarryOnlyTheirOwnSide()
    {
        using var document = JsonDocument.Parse(ChangeValueWriter.Write([ChangedValue.Added(Property("name"), "new"), ChangedValue.Removed(Property("quantity"), 4)])!);

        var created = document.RootElement[0];
        var removed = document.RootElement[1];
        Assert.Equal(["property", "kind", "after"], created.EnumerateObject().Select(member => member.Name));
        Assert.Equal(["property", "kind", "before"], removed.EnumerateObject().Select(member => member.Name));
        Assert.Equal("4", removed.GetProperty("before").GetString());
    }

    [Theory]
    [InlineData(ChangeValueKind.Text, "text")]
    [InlineData(ChangeValueKind.Number, "number")]
    [InlineData(ChangeValueKind.Boolean, "boolean")]
    [InlineData(ChangeValueKind.Date, "date")]
    [InlineData(ChangeValueKind.Instant, "instant")]
    [InlineData(ChangeValueKind.Enum, "enum")]
    [InlineData(ChangeValueKind.List, "list")]
    [InlineData(ChangeValueKind.Reference, "reference")]
    [InlineData(ChangeValueKind.Id, "id")]
    [InlineData(ChangeValueKind.Binary, "binary")]
    public void Write_Kind_IsItsCamelCaseName(ChangeValueKind kind, string written)
    {
        var value = Single(ChangedValue.Added(Property("name") with { Kind = kind }, "value"));

        Assert.Equal(written, value.GetProperty("kind").GetString());
    }

    private static RecordedProperty Property(string path)
    {
        using var context = new CaptureContext();

        return RecordedEntityType.Of(context.Model.FindEntityType(typeof(Widget))!).Properties.Single(property => property.Path == path);
    }

    private static JsonElement Single(ChangedValue value)
    {
        using var document = JsonDocument.Parse(ChangeValueWriter.Write([value])!);

        return Assert.Single(document.RootElement.EnumerateArray().ToList()).Clone();
    }
}
