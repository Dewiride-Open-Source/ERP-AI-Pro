using Dewiride.Erp.BuildingBlocks.Authentication.Graph;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.Graph;

public sealed class ProfilePhotosTests
{
    [Theory]
    [InlineData("FFD8FFE000104A464946", "image/jpeg")]
    [InlineData("89504E470D0A1A0A0000000D49484452", "image/png")]
    [InlineData("47494638396101000100", "image/gif")]
    [InlineData("47494638376101000100", "image/gif")]
    [InlineData("524946462400000057454250565038", "image/webp")]
    public void ContentTypeOf_BytesOfAnImageTheErpShows_AnswersItsType(string hex, string contentType)
    {
        Assert.Equal(contentType, ProfilePhotos.ContentTypeOf(Convert.FromHexString(hex)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("FFD8")]
    [InlineData("89504E470D0A1A")]
    [InlineData("474946383861")]
    [InlineData("524946462400000057415645")]
    [InlineData("3C68746D6C3E")]
    public void ContentTypeOf_BytesOfNoImageTheErpShows_AnswersNone(string hex)
    {
        Assert.Null(ProfilePhotos.ContentTypeOf(Convert.FromHexString(hex)));
    }

    [Fact]
    public async Task ReadAsync_PhotoOfTheLargestSizeTheErpKeeps_AnswersEveryByte()
    {
        var photo = new byte[ProfilePhotos.MaxBytes];
        photo[^1] = 0x2A;
        using var content = new ByteArrayContent(photo);

        var read = await ProfilePhotos.ReadAsync(content, TestContext.Current.CancellationToken);

        Assert.Equal(photo, read);
    }

    [Fact]
    public async Task ReadAsync_LargerPhotoWhoseLengthGraphSends_AnswersNone()
    {
        using var content = new ByteArrayContent(new byte[ProfilePhotos.MaxBytes + 1]);

        Assert.Null(await ProfilePhotos.ReadAsync(content, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_LargerPhotoWithoutALength_AnswersNoneOnceItPassesTheLimit()
    {
        using var content = new StreamContent(new MemoryStream(new byte[ProfilePhotos.MaxBytes + 1]));
        content.Headers.ContentLength = null;

        Assert.Null(await ProfilePhotos.ReadAsync(content, TestContext.Current.CancellationToken));
    }
}
