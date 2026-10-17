using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Auditing.Changes;

public interface IChangeHistory
{
    Task<Result<PagedResult<ChangeRecordDetails>>> ListAsync(ListRequest request, CancellationToken cancellationToken);

    Task<Result<ChangeExport>> ExportAsync(FilterRequest filter, CancellationToken cancellationToken);
}
