namespace Dewiride.Erp.BuildingBlocks.Application.Actors;

public sealed class SystemActorContext : IActorContext
{
    public Guid ActorId => ActorIds.System;

    public bool IsAuthenticated => false;
}
