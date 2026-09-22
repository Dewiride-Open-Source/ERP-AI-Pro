using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Actors;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Actors;

public sealed class ActorContextTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public void Services_ActorContext_IsTheHttpActorAndReportsTheSystemOutsideARequest()
    {
        using var scope = factory.Services.CreateScope();

        var actor = scope.ServiceProvider.GetRequiredService<IActorContext>();

        Assert.IsType<HttpActorContext>(actor);
        Assert.Equal(ActorIds.System, actor.ActorId);
        Assert.False(actor.IsAuthenticated);
        Assert.Single(scope.ServiceProvider.GetServices<IActorContext>());
    }
}
