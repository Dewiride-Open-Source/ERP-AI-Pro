using Dewiride.Erp.Modules.Identity.Users.Shared;

namespace Dewiride.Erp.Modules.Identity.Users.UnitTests.Shared;

public sealed class RecordVersionTests
{
    [Fact]
    public void TryDecode_EncodedRowVersion_ReturnsTheSameBytes()
    {
        byte[] rowVersion = [0, 0, 0, 0, 0, 0, 7, 209];

        Assert.True(RecordVersion.TryDecode(RecordVersion.Encode(rowVersion), out var decoded));
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
        Assert.False(RecordVersion.TryDecode(version, out _));
    }
}
