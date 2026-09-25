using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.Correlation;

public sealed class CorrelationIdTests
{
    [Theory]
    [InlineData("0199a1b2c3d4e5f60718293a4b5c6d7e")]
    [InlineData("order-4711")]
    [InlineData("web.request_1")]
    [InlineData("A")]
    public void IsWellFormed_ValueOfTheAllowedAlphabet_ReturnsTrue(string value) => Assert.True(CorrelationId.IsWellFormed(value));

    [Fact]
    public void IsWellFormed_ValueOfExactlyTheMaximumLength_ReturnsTrue() =>
        Assert.True(CorrelationId.IsWellFormed(new string('a', CorrelationId.MaxLength)));

    [Fact]
    public void IsWellFormed_ValueOneCharacterOverTheMaximumLength_ReturnsFalse() =>
        Assert.False(CorrelationId.IsWellFormed(new string('a', CorrelationId.MaxLength + 1)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    [InlineData("new\nline")]
    [InlineData("<script>")]
    [InlineData("slash/es")]
    public void IsWellFormed_ValueOutsideTheAllowedAlphabet_ReturnsFalse(string? value) => Assert.False(CorrelationId.IsWellFormed(value));

    [Fact]
    public void HeaderName_IsTheConventionalCorrelationHeader() => Assert.Equal("X-Correlation-ID", CorrelationId.HeaderName);
}
