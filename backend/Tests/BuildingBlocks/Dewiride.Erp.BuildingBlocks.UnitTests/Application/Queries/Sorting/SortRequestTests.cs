using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Application.Queries.Sorting;

public sealed class SortRequestTests
{
    private static readonly SortableFields<Row> Fields = new SortableFields<Row>().Add("name", r => r.Name).Add("age", r => r.Age);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_Empty_IsNone(string? text)
    {
        Assert.Same(SortRequest.None, SortRequest.Parse(text).Value);
    }

    [Theory]
    [InlineData("name", "name", SortDirection.Ascending)]
    [InlineData("name:asc", "name", SortDirection.Ascending)]
    [InlineData("name:DESC", "name", SortDirection.Descending)]
    [InlineData(" name : desc ", "name", SortDirection.Descending)]
    public void Parse_SingleTerm_ReadsFieldAndDirection(string text, string field, SortDirection direction)
    {
        var term = Assert.Single(SortRequest.Parse(text).Value.Terms);

        Assert.Equal(new SortTerm(field, direction), term);
    }

    [Fact]
    public void Parse_SeveralTerms_KeepsTheOrder()
    {
        var terms = SortRequest.Parse("age:desc,name").Value.Terms;

        Assert.Equal([new SortTerm("age", SortDirection.Descending), new SortTerm("name", SortDirection.Ascending)], terms);
    }

    [Theory]
    [InlineData("name:up")]
    [InlineData("name:asc:extra")]
    [InlineData("1name")]
    [InlineData("na-me")]
    [InlineData("name,")]
    [InlineData(",name")]
    [InlineData("name,NAME")]
    [InlineData("a,b,c,d,e,f")]
    public void Parse_Malformed_FailsWithInvalidSort(string text)
    {
        var result = SortRequest.Parse(text);

        Assert.Equal(QueryErrors.InvalidSort, result.Error!.Code);
    }

    [Fact]
    public void Resolve_KnownFields_ReturnsSelectorsInOrderIgnoringCase()
    {
        var resolved = SortRequest.Parse("AGE:desc,Name").Value.Resolve(Fields).Value;

        Assert.Equal(2, resolved.Terms.Count);
        Assert.Equal(typeof(int), resolved.Terms[0].Selector.ReturnType);
        Assert.Equal(SortDirection.Descending, resolved.Terms[0].Direction);
        Assert.Equal(typeof(string), resolved.Terms[1].Selector.ReturnType);
    }

    [Fact]
    public void Resolve_UnknownField_FailsNamingTheAllowedFields()
    {
        var result = SortRequest.Parse("city").Value.Resolve(Fields);

        Assert.Equal(QueryErrors.InvalidField, result.Error!.Code);
        Assert.Contains("name, age", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplySort_ResolvedTerms_OrdersWithTheTieBreakerLast()
    {
        Row[] rows = [new("b", 30, 1), new("a", 30, 2), new("c", 20, 3), new("a", 30, 0)];

        var sorted = rows.AsQueryable().ApplySort(SortRequest.Parse("age:desc,name").Value.Resolve(Fields).Value, r => r.Id).Select(r => r.Id).ToArray();

        Assert.Equal([0, 2, 1, 3], sorted);
    }

    [Fact]
    public void ApplySort_NoTerms_OrdersByTheTieBreakerOnly()
    {
        Row[] rows = [new("b", 1, 2), new("a", 1, 1)];

        var sorted = rows.AsQueryable().ApplySort(ResolvedSort<Row>.None, r => r.Id).Select(r => r.Id).ToArray();

        Assert.Equal([1, 2], sorted);
    }

    [Fact]
    public void Add_InvalidName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SortableFields<Row>().Add("first name", r => r.Name));
    }

    private sealed record Row(string Name, int Age, int Id);
}
