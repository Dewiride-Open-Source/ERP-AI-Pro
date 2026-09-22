using Dewiride.Erp.BuildingBlocks.Application.Actors;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Application.Actors;

public sealed class SystemActorContextTests
{
    [Fact]
    public void ActorId_IsTheReservedSystemActor()
    {
        var context = new SystemActorContext();

        Assert.Equal(ActorIds.System, context.ActorId);
        Assert.Equal(new Guid("00000000-0000-0000-0000-000000000001"), context.ActorId);
    }

    [Fact]
    public void IsAuthenticated_IsFalse()
    {
        var context = new SystemActorContext();

        Assert.False(context.IsAuthenticated);
    }

    [Fact]
    public void ActorIds_SystemAndAnonymous_AreDistinctReservedGuids()
    {
        Assert.Equal(new Guid("00000000-0000-0000-0000-000000000002"), ActorIds.Anonymous);
        Assert.NotEqual(ActorIds.System, ActorIds.Anonymous);
        Assert.NotEqual(Guid.Empty, ActorIds.System);
        Assert.NotEqual(Guid.Empty, ActorIds.Anonymous);
    }
}
