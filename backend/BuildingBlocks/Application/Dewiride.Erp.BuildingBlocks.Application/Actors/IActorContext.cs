namespace Dewiride.Erp.BuildingBlocks.Application.Actors;

public interface IActorContext
{
    Guid ActorId { get; }

    bool IsAuthenticated { get; }
}
