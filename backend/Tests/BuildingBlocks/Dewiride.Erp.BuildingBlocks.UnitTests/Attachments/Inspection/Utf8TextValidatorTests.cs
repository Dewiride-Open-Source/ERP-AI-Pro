using System.Text;
using Dewiride.Erp.BuildingBlocks.Attachments.Inspection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Inspection;

public sealed class Utf8TextValidatorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(64 * 1024)]
    public void AppendAndComplete_ValidTextSplitAtEveryPosition_IsAccepted(int partSize)
    {
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("a\u00e9\u20ac\ud834\udd1e\n", 20_000)));
        var validator = new Utf8TextValidator();

        for (var offset = 0; offset < bytes.Length; offset += partSize)
        {
            Assert.True(validator.Append(bytes.AsSpan(offset, Math.Min(partSize, bytes.Length - offset))));
        }

        Assert.True(validator.Complete());
    }

    [Fact]
    public void Complete_NothingAppended_IsAccepted()
    {
        Assert.True(new Utf8TextValidator().Complete());
    }

    [Theory]
    [InlineData(new byte[] { 0x61, 0x00, 0x62 })]
    [InlineData(new byte[] { 0x61, 0xFF, 0x62 })]
    [InlineData(new byte[] { 0x61, 0xC0, 0xAF })]
    [InlineData(new byte[] { 0xED, 0xA0, 0x80 })]
    [InlineData(new byte[] { 0x80 })]
    public void Append_NulOrInvalidUtf8_IsRefused(byte[] bytes)
    {
        Assert.False(new Utf8TextValidator().Append(bytes));
    }

    [Fact]
    public void Append_InvalidByteFarIntoTheText_IsRefused()
    {
        var validator = new Utf8TextValidator();
        Assert.True(validator.Append(Encoding.UTF8.GetBytes(new string('a', 100_000))));

        Assert.False(validator.Append([0x61, 0xFE]));
    }

    [Fact]
    public void Complete_TextEndingInsideACharacter_IsRefused()
    {
        var validator = new Utf8TextValidator();
        Assert.True(validator.Append([0x61, 0xE2, 0x82]));

        Assert.False(validator.Complete());
    }

    [Fact]
    public void AppendAndComplete_AfterARefusal_StayRefused()
    {
        var validator = new Utf8TextValidator();
        Assert.False(validator.Append([0x00]));

        Assert.False(validator.Append([0x61]));
        Assert.False(validator.Complete());
    }
}
