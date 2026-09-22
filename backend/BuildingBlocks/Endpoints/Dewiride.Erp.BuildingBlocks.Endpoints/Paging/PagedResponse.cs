namespace Dewiride.Erp.BuildingBlocks.Endpoints.Paging;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount, int TotalPages);
