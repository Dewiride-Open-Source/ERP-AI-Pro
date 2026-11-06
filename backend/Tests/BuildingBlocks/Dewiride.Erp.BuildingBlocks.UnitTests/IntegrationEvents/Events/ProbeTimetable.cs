using Dewiride.Erp.BuildingBlocks.Kernel.Events;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.IntegrationEvents.Events;

public sealed record ProbeTimetable(Guid TimetableId, IReadOnlyList<DateTimeOffset> Starts) : IIntegrationEvent
{
    public static string Name => "test.probes.timetable-published";
}
