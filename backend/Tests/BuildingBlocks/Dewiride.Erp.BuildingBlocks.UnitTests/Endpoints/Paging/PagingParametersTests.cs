using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.Paging;

public sealed class PagingParametersTests
{
    [Fact]
    public void ToListRequest_EveryPartSupplied_LandsInTheMatchingSlot()
    {
        var request = new PagingParameters(3, 25, "name:desc", "city:eq:Pune").ToListRequest().Value;

        Assert.Equal(3, request.Page.Page);
        Assert.Equal(25, request.Page.PageSize);
        Assert.Equal(new SortTerm("name", SortDirection.Descending), Assert.Single(request.Sort.Terms));
        var filter = Assert.Single(request.Filter.Terms);
        Assert.Equal("city", filter.Field);
        Assert.Equal(FilterOperator.Equal, filter.Operator);
        Assert.Equal(["Pune"], filter.Values);
    }

    [Fact]
    public void ToListRequest_NothingSupplied_IsTheDefaultRequest()
    {
        Assert.Equal(ListRequest.Default, new PagingParameters(null, null, null, null).ToListRequest().Value);
    }

    [Theory]
    [InlineData(0, null, null, null, QueryErrors.InvalidPage)]
    [InlineData(null, 500, null, null, QueryErrors.InvalidPage)]
    [InlineData(null, null, "name:sideways", null, QueryErrors.InvalidSort)]
    [InlineData(null, null, null, "city", QueryErrors.InvalidFilter)]
    public void ToListRequest_MalformedPart_FailsWithThatPartsCode(int? page, int? pageSize, string? sort, string? filter, string expectedCode)
    {
        var result = new PagingParameters(page, pageSize, sort, filter).ToListRequest();

        Assert.Equal(expectedCode, result.Error!.Code);
    }

    [Fact]
    public void ToResponse_PagedResult_CarriesTheItemsAndTheTotals()
    {
        var response = new PagedResult<string>(["a", "b"], 2, 2, 5).ToResponse();

        Assert.Equal(["a", "b"], response.Items);
        Assert.Equal(2, response.Page);
        Assert.Equal(2, response.PageSize);
        Assert.Equal(5, response.TotalCount);
        Assert.Equal(3, response.TotalPages);
    }

    [Fact]
    public void ToResponse_WithAMap_MapsEveryItemAndKeepsTheTotals()
    {
        var response = new PagedResult<int>([1, 2], 1, 2, 4).ToResponse(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(["1", "2"], response.Items);
        Assert.Equal(1, response.Page);
        Assert.Equal(4, response.TotalCount);
        Assert.Equal(2, response.TotalPages);
    }
}
