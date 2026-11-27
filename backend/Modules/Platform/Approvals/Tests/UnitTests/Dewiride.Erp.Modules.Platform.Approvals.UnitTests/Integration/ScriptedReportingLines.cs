using Dewiride.Erp.Modules.Identity.Users.Contracts.Positions;

namespace Dewiride.Erp.Modules.Platform.Approvals.UnitTests.Integration;

internal sealed class ScriptedReportingLines(params ReportingManagerEntry[] managers) : IReportingLines
{
    public List<(Guid ObjectId, DateOnly On)> Asked { get; } = [];

    public Task<IReadOnlyList<ReportingManagerEntry>> ManagersOfAsync(Guid objectId, DateOnly on, CancellationToken cancellationToken)
    {
        Asked.Add((objectId, on));

        return Task.FromResult<IReadOnlyList<ReportingManagerEntry>>(managers);
    }
}
