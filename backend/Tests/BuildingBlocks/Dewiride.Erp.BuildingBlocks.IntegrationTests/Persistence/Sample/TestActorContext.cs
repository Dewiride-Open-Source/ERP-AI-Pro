using Dewiride.Erp.BuildingBlocks.Application.Actors;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;

public sealed class TestActorContext : IActorContext
{
    public Guid ActorId { get; set; } = ActorIds.System;

    public bool IsAuthenticated => ActorId != ActorIds.System && ActorId != ActorIds.Anonymous;
}
