using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;
using Dewiride.Erp.BuildingBlocks.Kernel.Events;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.IntegrationEvents.Events;

public sealed class IntegrationEventNamesTests
{
    [Theory]
    [InlineData("identity.users.person-deactivated", true)]
    [InlineData("hr.core.joiner2-arrived", true)]
    [InlineData("identity.users", false)]
    [InlineData("identity.users.person.deactivated", false)]
    [InlineData("Identity.users.person-deactivated", false)]
    [InlineData("identity.users.person_deactivated", false)]
    [InlineData("identity.users.-deactivated", false)]
    [InlineData("identity.users.person-deactivated\n", false)]
    [InlineData("", false)]
    public void IsWellFormed_Name_IsThreeLowerCaseKebabSegments(string name, bool wellFormed)
    {
        Assert.Equal(wellFormed, IntegrationEventNames.IsWellFormed(name));
    }

    [Fact]
    public void IsWellFormed_NameOfTheMaximumLength_IsAcceptedAndOneCharacterMoreIsRefused()
    {
        var longest = $"a.b.{new string('c', IntegrationEventNames.MaxLength - 4)}";

        Assert.True(IntegrationEventNames.IsWellFormed(longest));
        Assert.False(IntegrationEventNames.IsWellFormed($"{longest}c"));
    }

    [Fact]
    public void Of_EventWithAMalformedName_ThrowsNamingTheEvent()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => IntegrationEventNames.Of<MisnamedEvent>());

        Assert.Contains(nameof(MisnamedEvent), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Of_EventWithAWellFormedName_AnswersIt()
    {
        Assert.Equal("test.probes.thing-happened", IntegrationEventNames.Of<ProbeEvent>());
    }

    private sealed record MisnamedEvent : IIntegrationEvent
    {
        public static string Name => "Test.Probes.Misnamed";
    }
}
