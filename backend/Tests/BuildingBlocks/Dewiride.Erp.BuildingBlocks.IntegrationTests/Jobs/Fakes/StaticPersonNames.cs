using Dewiride.Erp.BuildingBlocks.Application.Actors;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;

public sealed class StaticPersonNames(IReadOnlyDictionary<Guid, string> names) : IPersonNames
{
    public Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(IReadOnlyCollection<Guid> objectIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objectIds);

        IReadOnlyDictionary<Guid, string> found = objectIds.Where(names.ContainsKey).ToDictionary(id => id, id => names[id]);

        return Task.FromResult(found);
    }
}
