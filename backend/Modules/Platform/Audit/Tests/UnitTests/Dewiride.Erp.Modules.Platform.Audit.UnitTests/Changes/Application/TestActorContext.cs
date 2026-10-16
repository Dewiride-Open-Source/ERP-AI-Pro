using Dewiride.Erp.BuildingBlocks.Application.Actors;

namespace Dewiride.Erp.Modules.Platform.Audit.UnitTests.Changes.Application;

internal sealed record TestActorContext(Guid ActorId, bool IsAuthenticated) : IActorContext;
