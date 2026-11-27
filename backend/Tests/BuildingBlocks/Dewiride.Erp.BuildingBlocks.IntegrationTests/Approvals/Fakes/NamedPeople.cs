using System.Collections.Concurrent;
using Dewiride.Erp.BuildingBlocks.Application.Actors;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Fakes;

internal sealed class NamedPeople : IPersonNames
{
    private readonly ConcurrentDictionary<Guid, string> _names = new();

    public void Name(Guid person, string name) => _names[person] = name;

    public Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(IReadOnlyCollection<Guid> objectIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objectIds);

        return Task.FromResult<IReadOnlyDictionary<Guid, string>>(objectIds.Where(_names.ContainsKey).Distinct().ToDictionary(person => person, person => _names[person]));
    }
}
