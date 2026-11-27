using System.Collections.Concurrent;
using Dewiride.Erp.BuildingBlocks.Approvals.Routing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Approvals;

// Answers each person's managers as the test wrote them, nearest first, and keeps every question it was asked.
internal sealed class ScriptedReportingManagers : IReportingManagers
{
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<ReportingManager>> _chains = new();

    public ConcurrentQueue<(Guid Person, DateOnly On)> Asked { get; } = new();

    public void Set(Guid person, params ReportingManager[] chain) => _chains[person] = chain;

    public Task<IReadOnlyList<ReportingManager>> ManagersOfAsync(Guid objectId, DateOnly on, CancellationToken cancellationToken)
    {
        Asked.Enqueue((objectId, on));

        return Task.FromResult(_chains.GetValueOrDefault(objectId) ?? []);
    }
}
