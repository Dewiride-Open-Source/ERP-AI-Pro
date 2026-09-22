using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Application.Queries.Paging;

public sealed class PageRequestTests
{
    [Fact]
    public void Create_NoValues_IsTheFirstPageOfFifty()
    {
        var page = PageRequest.Create(null, null).Value;

        Assert.Equal(PageRequest.First, page);
        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
        Assert.Equal(0, page.Skip);
    }

    [Theory]
    [InlineData(1, 200, 0)]
    [InlineData(3, 25, 50)]
    [InlineData(1000, 1, 999)]
    public void Create_ValuesInRange_ComputesTheSkip(int page, int pageSize, int expectedSkip)
    {
        var request = PageRequest.Create(page, pageSize).Value;

        Assert.Equal(expectedSkip, request.Skip);
    }

    [Theory]
    [InlineData(1, 200, 0)]
    [InlineData(10737419, 200, 2147483600)]
    [InlineData(int.MaxValue, 1, 2147483646)]
    public void Create_HighestPageForThePageSize_StillComputesAPositiveSkip(int page, int pageSize, int expectedSkip)
    {
        Assert.Equal(expectedSkip, PageRequest.Create(page, pageSize).Value.Skip);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 201)]
    [InlineData(10737420, 200)]
    [InlineData(int.MaxValue, 200)]
    public void Create_ValuesOutOfRange_FailsWithInvalidPage(int page, int pageSize)
    {
        var result = PageRequest.Create(page, pageSize);

        Assert.Equal(QueryErrors.InvalidPage, result.Error!.Code);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 201)]
    [InlineData(10737420, 200)]
    public void Constructor_ValuesOutOfRange_Throws(int page, int pageSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PageRequest(page, pageSize));
    }

    [Fact]
    public void PagedResult_TotalsAndNavigation_FollowTheCount()
    {
        var result = new PagedResult<string>(["a", "b"], 2, 2, 5);

        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasPrevious);
        Assert.True(result.HasNext);
        Assert.Equal(["A", "B"], result.Map(item => item.ToUpperInvariant()).Items);
        Assert.False(new PagedResult<string>([], 1, 50, 0).HasNext);
    }

    [Fact]
    public void ListRequest_Parse_CombinesPageSortAndFilter()
    {
        var request = ListRequest.Parse(2, 10, "name:desc", "name:contains:al").Value;

        Assert.Equal(2, request.Page.Page);
        Assert.Equal("name", Assert.Single(request.Sort.Terms).Field);
        Assert.Equal("al", Assert.Single(request.Filter.Terms).Values[0]);
        Assert.Equal(ListRequest.Default, ListRequest.Parse(null, null, null, null).Value);
    }

    [Theory]
    [InlineData(0, null, null, null, QueryErrors.InvalidPage)]
    [InlineData(null, null, "name:up", null, QueryErrors.InvalidSort)]
    [InlineData(null, null, null, "name=x", QueryErrors.InvalidFilter)]
    public void ListRequest_Parse_ReportsTheFirstInvalidPart(int? page, int? pageSize, string? sort, string? filter, string expectedCode)
    {
        var result = ListRequest.Parse(page, pageSize, sort, filter);

        Assert.Equal(expectedCode, result.Error!.Code);
    }
}
