using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Audit.IntegrationTests.Changes.Endpoints.Export;

// The history refuses an export above its cap only once that many lines exist, so a test hands the refusal over instead of
// writing them.
internal sealed class StubChangeHistory(Result<ChangeExport> export) : IChangeHistory
{
    public Task<Result<PagedResult<ChangeRecordDetails>>> ListAsync(ListRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<Result<PagedResult<ChangeRecordDetails>>>(new PagedResult<ChangeRecordDetails>([], 1, PageRequest.DefaultPageSize, 0));

    public Task<Result<ChangeExport>> ExportAsync(FilterRequest filter, CancellationToken cancellationToken) => Task.FromResult(export);
}
