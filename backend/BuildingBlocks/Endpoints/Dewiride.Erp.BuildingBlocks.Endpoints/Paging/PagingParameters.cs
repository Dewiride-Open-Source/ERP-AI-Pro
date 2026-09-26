using System.ComponentModel;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Paging;

public sealed record PagingParameters(
    [property: FromQuery(Name = "page")]
    [property: Description("Page number, from 1; the first page when absent.")]
    int? Page,
    [property: FromQuery(Name = "pageSize")]
    [property: Description("Items per page, from 1 to 200; 50 when absent.")]
    int? PageSize,
    [property: FromQuery(Name = "sort")]
    [property: Description("Comma-separated field:asc or field:desc terms over the fields the endpoint allows.")]
    string? Sort,
    [property: FromQuery(Name = "filter")]
    [property: Description("Semicolon-separated field:operator:value terms over the fields the endpoint allows; values are percent-encoded and an in-list separates its values with |.")]
    string? Filter)
{
    public Result<ListRequest> ToListRequest() => ListRequest.Parse(Page, PageSize, Sort, Filter);
}
