using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.IntegrationEvents.Subscribing;

public sealed class IntegrationEventCatalogTests
{
    [Fact]
    public void SubscribersOf_EventWithSubscribers_AnswersThemByName()
    {
        var catalog = new IntegrationEventCatalog([Subscription("hr.core.joiner-checklist", typeof(int)), Subscription("finance.payroll.joiner-setup", typeof(long)), Subscription("hr.core.leaver-checklist", typeof(short), "identity.users.person-left")]);

        Assert.Equal(["finance.payroll.joiner-setup", "hr.core.joiner-checklist"], catalog.SubscribersOf("identity.users.person-joined").Select(subscription => subscription.Subscriber));
        Assert.Empty(catalog.SubscribersOf("identity.users.person-moved"));
    }

    [Fact]
    public void Constructor_HandlerRegisteredTwice_ThrowsNamingIt()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new IntegrationEventCatalog([Subscription("hr.core.joiner-checklist", typeof(int)), Subscription("hr.core.joiner-welcome", typeof(int))]));

        Assert.Contains(nameof(Int32), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Of_HandlerNotRegistered_Throws()
    {
        var catalog = new IntegrationEventCatalog([Subscription("hr.core.joiner-checklist", typeof(int))]);

        Assert.Equal("hr.core.joiner-checklist", catalog.Of(typeof(int)).Subscriber);
        Assert.Throws<InvalidOperationException>(() => catalog.Of(typeof(long)));
    }

    private static IntegrationEventSubscription Subscription(string subscriber, Type handler, string eventName = "identity.users.person-joined") =>
        new(eventName, subscriber, handler, static (_, _, _, _) => Task.FromResult(JobRunId.Create()));
}
