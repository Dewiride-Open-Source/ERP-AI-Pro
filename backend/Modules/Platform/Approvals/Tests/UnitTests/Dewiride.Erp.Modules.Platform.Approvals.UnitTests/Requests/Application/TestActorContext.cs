using Dewiride.Erp.BuildingBlocks.Application.Actors;

namespace Dewiride.Erp.Modules.Platform.Approvals.UnitTests.Requests.Application;

internal sealed record TestActorContext(Guid ActorId, bool IsAuthenticated) : IActorContext;
