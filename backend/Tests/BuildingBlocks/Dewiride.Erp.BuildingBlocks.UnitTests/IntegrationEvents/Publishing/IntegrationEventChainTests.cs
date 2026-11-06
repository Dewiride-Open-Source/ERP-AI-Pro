using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Publishing;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.IntegrationEvents.Publishing;

public sealed class IntegrationEventChainTests
{
    private const string Delivered = "test.probes.thing-happened";

    private const string Next = "test.probes.thing-followed";

    [Fact]
    public void DepthOf_OutsideADelivery_IsZero()
    {
        Assert.Equal(0, new IntegrationEventChain().DepthOf(Next));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(IntegrationEventChain.MaxDepth - 1, IntegrationEventChain.MaxDepth)]
    public void DepthOf_InsideADelivery_IsOneMoreThanTheDeliveredEvent(int delivered, int expected)
    {
        var chain = new IntegrationEventChain();
        chain.Deliver(DeliveryAt(delivered));

        Assert.Equal(expected, chain.DepthOf(Next));
    }

    [Fact]
    public void DepthOf_PastTheLongestChain_IsRefusedNamingBothEvents()
    {
        var chain = new IntegrationEventChain();
        chain.Deliver(DeliveryAt(IntegrationEventChain.MaxDepth));

        var refusal = Assert.Throws<InvalidOperationException>(() => chain.DepthOf(Next));

        Assert.StartsWith($"{Next} would follow a chain of {IntegrationEventChain.MaxDepth + 1} deliveries", refusal.Message, StringComparison.Ordinal);
        Assert.Contains(Delivered, refusal.Message, StringComparison.Ordinal);
    }

    private static IntegrationEventDelivery DeliveryAt(int depth) =>
        new(Guid.CreateVersion7(), Delivered, "test_sample", DateTimeOffset.UnixEpoch, null, depth, default);
}
