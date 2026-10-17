using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Auditing.Changes;

public sealed class StoredChangesTests
{
    [Fact]
    public void Parse_RecordWithoutValues_ReadsNone()
    {
        Assert.Empty(StoredChanges.Parse(null));
    }

    [Fact]
    public void Parse_CreatedRecord_ReadsEachValueAfterTheChangeOnly()
    {
        var values = StoredChanges.Parse("""[{"property":"displayName","kind":"text","after":"Asha Rao"},{"property":"dateOfJoining","kind":"date","after":"2026-04-01"}]""");

        Assert.Equal(
            [new StoredChange("displayName", ChangeValueKind.Text, null, "Asha Rao", null), new StoredChange("dateOfJoining", ChangeValueKind.Date, null, "2026-04-01", null)],
            values);
    }

    [Fact]
    public void Parse_RemovedRecord_ReadsEachValueBeforeTheChangeOnly()
    {
        var value = Assert.Single(StoredChanges.Parse("""[{"property":"effectiveFrom","kind":"date","before":"2026-04-01"}]"""));

        Assert.Equal(new StoredChange("effectiveFrom", ChangeValueKind.Date, "2026-04-01", null, null), value);
    }

    [Fact]
    public void Parse_UpdatedValueThatWasEmpty_ReadsNothingBeforeIt()
    {
        var value = Assert.Single(StoredChanges.Parse("""[{"property":"phoneNumber","kind":"text","before":null,"after":"+919812345678"}]"""));

        Assert.Null(value.Before);
        Assert.Equal("+919812345678", value.After);
    }

    [Fact]
    public void Parse_Reference_ReadsTheChangeKeyOfTheReferencedType()
    {
        var value = Assert.Single(StoredChanges.Parse(
            """[{"property":"departmentId","kind":"reference","before":null,"after":"0192f5f0-58a1-7c2e-9d3b-6f1e2a4b5c6d","references":"identity.users.departments"}]"""));

        Assert.Equal(new StoredChange("departmentId", ChangeValueKind.Reference, null, "0192f5f0-58a1-7c2e-9d3b-6f1e2a4b5c6d", "identity.users.departments"), value);
    }

    [Fact]
    public void Parse_List_KeepsItAsCompactJsonWithTheLettersOfEveryScriptUnescaped()
    {
        var value = Assert.Single(StoredChanges.Parse("""[{"property":"aliases","kind":"list","before":["finance"],"after":["finance", "आशा"]}]"""));

        Assert.Equal("""["finance"]""", value.Before);
        Assert.Equal("""["finance","आशा"]""", value.After);
    }

    [Theory]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    public void Parse_Boolean_ReadsItsJsonText(string stored, string expected)
    {
        var value = Assert.Single(StoredChanges.Parse($$"""[{"property":"isPrimary","kind":"boolean","before":null,"after":{{stored}}}]"""));

        Assert.Equal(expected, value.After);
    }

    [Theory]
    [InlineData("text", ChangeValueKind.Text)]
    [InlineData("number", ChangeValueKind.Number)]
    [InlineData("boolean", ChangeValueKind.Boolean)]
    [InlineData("date", ChangeValueKind.Date)]
    [InlineData("instant", ChangeValueKind.Instant)]
    [InlineData("enum", ChangeValueKind.Enum)]
    [InlineData("list", ChangeValueKind.List)]
    [InlineData("reference", ChangeValueKind.Reference)]
    [InlineData("id", ChangeValueKind.Id)]
    [InlineData("binary", ChangeValueKind.Binary)]
    public void Parse_Kind_IsReadFromItsCamelCaseName(string stored, ChangeValueKind expected)
    {
        var value = Assert.Single(StoredChanges.Parse($$"""[{"property":"field","kind":"{{stored}}","after":null}]"""));

        Assert.Equal(expected, value.Kind);
    }

    [Theory]
    [InlineData("Text")]
    [InlineData("money")]
    [InlineData("1")]
    [InlineData("text, number")]
    public void Parse_KindThatIsNotACamelCaseName_Throws(string stored)
    {
        Assert.Throws<JsonException>(() => StoredChanges.Parse($$"""[{"property":"field","kind":"{{stored}}","after":"x"}]"""));
    }

    [Theory]
    [InlineData("""{"property":"field","kind":"text"}""")]
    [InlineData("""[{"kind":"text","after":"x"}]""")]
    [InlineData("""[{"property":7,"kind":"text","after":"x"}]""")]
    [InlineData("""[{"property":"field","kind":"reference","after":"x","references":3}]""")]
    [InlineData("""["field"]""")]
    public void Parse_EntriesNotShapedAsTheCaptureWritesThem_Throws(string stored)
    {
        Assert.Throws<JsonException>(() => StoredChanges.Parse(stored));
    }
}
