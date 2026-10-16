using System.Runtime.CompilerServices;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Audit.UnitTests.Changes.Application;

internal sealed class RecordingChangeHistory : IChangeHistory
{
    public static ChangeExport SampleExport { get; } = new(3, 5, new DateTimeOffset(2026, 10, 16, 9, 30, 0, TimeSpan.Zero), NoRecordsAsync);

    public Result<PagedResult<ChangeRecordDetails>> Page { get; init; } = new PagedResult<ChangeRecordDetails>([], 1, 50, 0);

    public Result<ChangeExport> Export { get; init; } = SampleExport;

    public ListRequest? ReceivedRequest { get; private set; }

    public FilterRequest? ReceivedFilter { get; private set; }

    public CancellationToken ReceivedCancellation { get; private set; }

    public Task<Result<PagedResult<ChangeRecordDetails>>> ListAsync(ListRequest request, CancellationToken cancellationToken)
    {
        ReceivedRequest = request;
        ReceivedCancellation = cancellationToken;

        return Task.FromResult(Page);
    }

    public Task<Result<ChangeExport>> ExportAsync(FilterRequest filter, CancellationToken cancellationToken)
    {
        ReceivedFilter = filter;
        ReceivedCancellation = cancellationToken;

        return Task.FromResult(Export);
    }

    private static async IAsyncEnumerable<ChangeRecordDetails> NoRecordsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        yield break;
    }
}
