using Dewiride.Erp.BuildingBlocks.Kernel.Events;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.IntegrationEvents.Events;

public sealed record ProbeEvent(Guid SubjectId, int Smallest, int Largest, DateTimeOffset At, DateOnly On, ProbeKind Kind, IReadOnlyList<Guid> Members, Guid? ManagerId = null) : IIntegrationEvent
{
    public static string Name => "test.probes.thing-happened";
}
