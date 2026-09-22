using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Application.Queries.Filtering;

public sealed class FilterRequestTests
{
    private static readonly FilterableFields<Row> Fields = new FilterableFields<Row>()
        .Add("name", r => r.Name)
        .Add("age", r => r.Age)
        .Add("active", r => r.Active)
        .Add("id", r => r.Id)
        .Add("born", r => r.Born)
        .Add("status", r => r.Status)
        .Add("score", r => r.Score)
        .Add("code", r => r.Code, FilterOperator.Equal);

    private static readonly Row[] Rows =
    [
        new("Alice", 30, true, RowId.From(new Guid("11111111-1111-1111-1111-111111111111")), new DateOnly(1996, 4, 1), Status.Open, 1.5m, "a;b"),
        new("Bob", 25, false, RowId.From(new Guid("22222222-2222-2222-2222-222222222222")), new DateOnly(2001, 3, 31), Status.Closed, null, "x:y"),
        new("Carol", 41, true, RowId.From(new Guid("33333333-3333-3333-3333-333333333333")), new DateOnly(1985, 1, 15), Status.Open, 9m, "p|q"),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Parse_Empty_IsNone(string? text)
    {
        Assert.Same(FilterRequest.None, FilterRequest.Parse(text).Value);
    }

    [Fact]
    public void Parse_TermsWithEncodedSeparatorsInsideValues_KeepsTheValuesIntact()
    {
        var terms = FilterRequest.Parse("code:eq:a%3Bb;name:in:x%3Ay|p%7Cq|plain; age : GTE : 30").Value.Terms;

        Assert.Equal(["code", "name", "age"], terms.Select(term => term.Field));
        Assert.Equal([FilterOperator.Equal, FilterOperator.In, FilterOperator.GreaterThanOrEqual], terms.Select(term => term.Operator));
        Assert.Equal(["a;b"], terms[0].Values);
        Assert.Equal(["x:y", "p|q", "plain"], terms[1].Values);
        Assert.Equal(["30"], terms[2].Values);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("name:eq")]
    [InlineData("name:like:x")]
    [InlineData("na me:eq:x")]
    [InlineData("name:in:")]
    [InlineData("name:in:|")]
    [InlineData("name:eq:x;")]
    [InlineData("a:eq:1;b:eq:1;c:eq:1;d:eq:1;e:eq:1;f:eq:1;g:eq:1;h:eq:1;i:eq:1;j:eq:1;k:eq:1")]
    public void Parse_Malformed_FailsWithInvalidFilter(string text)
    {
        Assert.Equal(QueryErrors.InvalidFilter, FilterRequest.Parse(text).Error!.Code);
    }

    [Fact]
    public void Parse_EqualWithAnEmptyValue_IsAllowed()
    {
        var term = Assert.Single(FilterRequest.Parse("name:eq:").Value.Terms);

        Assert.Equal([""], term.Values);
    }

    [Theory]
    [InlineData("name:contains:o", "Bob,Carol")]
    [InlineData("name:eq:Alice", "Alice")]
    [InlineData("name:ne:Alice", "Bob,Carol")]
    [InlineData("name:in:Bob|Carol|Nobody", "Bob,Carol")]
    [InlineData("name:in: Bob | Carol ", "Bob,Carol")]
    [InlineData("score:in:1.5 | 9", "Alice,Carol")]
    [InlineData("age:gt:25;age:lt:41", "Alice")]
    [InlineData("age:gte:30", "Alice,Carol")]
    [InlineData("age:lte:25", "Bob")]
    [InlineData("active:eq:true", "Alice,Carol")]
    [InlineData("active:ne:true", "Bob")]
    [InlineData("id:eq:22222222-2222-2222-2222-222222222222", "Bob")]
    [InlineData("id:in:11111111-1111-1111-1111-111111111111|33333333-3333-3333-3333-333333333333", "Alice,Carol")]
    [InlineData("born:gte:1996-04-01", "Alice,Bob")]
    [InlineData("born:lt:1996-04-01", "Carol")]
    [InlineData("status:eq:open", "Alice,Carol")]
    [InlineData("status:in:closed", "Bob")]
    [InlineData("score:gt:2", "Carol")]
    [InlineData("code:eq:x%3Ay", "Bob")]
    public void Resolve_KnownFieldsAndOperators_FiltersTheRows(string text, string expected)
    {
        var filter = FilterRequest.Parse(text).Value.Resolve(Fields).Value;

        var names = Rows.AsQueryable().ApplyFilter(filter).Select(r => r.Name).ToArray();

        Assert.Equal(expected.Split(','), names);
    }

    [Fact]
    public void Resolve_NoTerms_LeavesTheQueryUntouched()
    {
        var source = Rows.AsQueryable();

        Assert.Same(source, source.ApplyFilter(FilterRequest.None.Resolve(Fields).Value));
    }

    [Theory]
    [InlineData("city:eq:Pune", QueryErrors.InvalidField, "name, age")]
    [InlineData("name:gt:a", QueryErrors.InvalidOperator, "eq, ne, in, contains")]
    [InlineData("active:contains:t", QueryErrors.InvalidOperator, "eq, ne")]
    [InlineData("id:gt:11111111-1111-1111-1111-111111111111", QueryErrors.InvalidOperator, "eq, ne, in")]
    [InlineData("status:contains:op", QueryErrors.InvalidOperator, "eq, ne, in")]
    [InlineData("code:contains:a", QueryErrors.InvalidOperator, "eq")]
    [InlineData("age:eq:old", QueryErrors.InvalidValue, "Int32")]
    [InlineData("active:eq:yes", QueryErrors.InvalidValue, "Boolean")]
    [InlineData("id:eq:not-a-guid", QueryErrors.InvalidValue, "RowId")]
    [InlineData("born:eq:01/04/1996", QueryErrors.InvalidValue, "DateOnly")]
    [InlineData("status:eq:pending", QueryErrors.InvalidValue, "Status")]
    [InlineData("score:eq:1,5", QueryErrors.InvalidValue, "Decimal")]
    [InlineData("age:in:1|two", QueryErrors.InvalidValue, "Int32")]
    public void Resolve_UnknownFieldOperatorOrValue_FailsNamingTheProblem(string text, string expectedCode, string expectedFragment)
    {
        var result = FilterRequest.Parse(text).Value.Resolve(Fields);

        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.Contains(expectedFragment, result.Error.Message, StringComparison.Ordinal);
    }

    private enum Status
    {
        Open,
        Closed,
    }

    private readonly record struct RowId(Guid Value) : IStronglyTypedId<RowId>
    {
        public static RowId Create() => new(Guid.CreateVersion7());

        public static RowId From(Guid value) => new(value);
    }

    private sealed record Row(string Name, int Age, bool Active, RowId Id, DateOnly Born, Status Status, decimal? Score, string Code);
}
