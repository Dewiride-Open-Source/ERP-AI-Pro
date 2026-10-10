using Dewiride.Erp.Modules.Identity.Users.People.Application;

namespace Dewiride.Erp.Modules.Identity.Users.UnitTests.People.Application;

public sealed class PersonVersionTests
{
    [Fact]
    public void TryDecode_EncodedRowVersion_ReturnsTheSameBytes()
    {
        byte[] rowVersion = [0, 0, 0, 0, 0, 0, 7, 209];

        Assert.True(PersonVersion.TryDecode(PersonVersion.Encode(rowVersion), out var decoded));
        Assert.Equal(rowVersion, decoded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("AAAAAAAAB9E=AA")]
    [InlineData("AAAAAAAH")]
    [InlineData("AAAAAAAAAAAH0Q==")]
    public void TryDecode_ValueThatIsNoRowVersion_ReturnsFalse(string? version)
    {
        Assert.False(PersonVersion.TryDecode(version, out _));
    }
}
