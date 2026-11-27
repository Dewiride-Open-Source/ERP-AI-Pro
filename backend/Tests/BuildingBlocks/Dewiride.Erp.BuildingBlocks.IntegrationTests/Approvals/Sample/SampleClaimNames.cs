using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample;

public sealed class SampleClaimNames(SampleDbContext context) : IRecordNames
{
    public IReadOnlySet<string> EntityTypes { get; } = new HashSet<string>(StringComparer.Ordinal) { SampleClaimConfiguration.ChangeKey };

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(string entityType, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var claimIds = ids.Select(SampleClaimId.From).ToList();

        return await context.Claims.AsNoTracking().Where(c => claimIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id.Value, c => c.Title, cancellationToken);
    }
}
