using System.Runtime.CompilerServices;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Audit.IntegrationTests.Changes.Endpoints.Export;

// Holds every export at its start, inside the endpoint and so inside its rate-limiter lease, until released, so a test knows
// how many exports are in flight.
internal sealed class HeldChangeHistory(int expectedExports) : IChangeHistory
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _exports;

    public Task Entered => _entered.Task;

    public void Release() => _released.TrySetResult();

    public Task<Result<PagedResult<ChangeRecordDetails>>> ListAsync(ListRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<Result<PagedResult<ChangeRecordDetails>>>(new PagedResult<ChangeRecordDetails>([], 1, PageRequest.DefaultPageSize, 0));

    public async Task<Result<ChangeExport>> ExportAsync(FilterRequest filter, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _exports) == expectedExports)
        {
            _entered.TrySetResult();
        }

        await _released.Task.WaitAsync(cancellationToken);

        return new ChangeExport(0, 0, TimeProvider.System.GetUtcNow(), NoRecordsAsync);
    }

    private static async IAsyncEnumerable<ChangeRecordDetails> NoRecordsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        yield break;
    }
}
