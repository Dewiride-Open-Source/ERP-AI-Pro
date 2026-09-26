using System.Buffers.Binary;
using System.Security.Cryptography;
using Azure;
using Azure.Storage;
using Dewiride.Erp.BuildingBlocks.Attachments.Storage.Blob;
using Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Fakes;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Storage.Blob;

public sealed class BlockStagingStreamTests
{
    private const int BlockSize = 4 * 1024 * 1024;

    private readonly RecordingBlockBlobClient _blob = new();

    [Fact]
    public async Task WriteAsync_LessThanABlock_StagesNothing()
    {
        await using var stream = new BlockStagingStream(_blob);

        await stream.WriteAsync(RandomNumberGenerator.GetBytes(BlockSize - 1), TestContext.Current.CancellationToken);

        Assert.Empty(_blob.StagedBlocks);
        Assert.Empty(_blob.Commits);
    }

    [Fact]
    public async Task WriteAsync_ExactlyOneBlock_StagesItAtOnce()
    {
        var data = RandomNumberGenerator.GetBytes(BlockSize);
        await using var stream = new BlockStagingStream(_blob);

        await stream.WriteAsync(data, TestContext.Current.CancellationToken);

        var staged = Assert.Single(_blob.StagedBlocks);
        Assert.Equal(data, staged.Content);
        Assert.Empty(_blob.Commits);
    }

    [Fact]
    public async Task WriteAsync_OddWriteSizes_StagesOneBlockPerFourMebibytes()
    {
        var data = RandomNumberGenerator.GetBytes((2 * BlockSize) + 3);
        await using var stream = new BlockStagingStream(_blob);

        for (var offset = 0; offset < data.Length; offset += 1_000_003)
        {
            await stream.WriteAsync(data.AsMemory(offset, Math.Min(1_000_003, data.Length - offset)), TestContext.Current.CancellationToken);
        }

        Assert.Equal(2, _blob.StagedBlocks.Count);
        Assert.All(_blob.StagedBlocks, block => Assert.Equal(BlockSize, block.Content.Length));
        Assert.Equal(data[..(2 * BlockSize)], _blob.StagedBlocks.SelectMany(block => block.Content).ToArray());
    }

    [Fact]
    public async Task WriteAsync_ArrayOverloadWithAnOffset_StagesOnlyThatRange()
    {
        var data = RandomNumberGenerator.GetBytes(BlockSize + 10);
        await using var stream = new BlockStagingStream(_blob);

#pragma warning disable CA1835 // The array overload is the behaviour under test.
        await stream.WriteAsync(data, 10, BlockSize, TestContext.Current.CancellationToken);
#pragma warning restore CA1835

        Assert.Equal(data[10..], Assert.Single(_blob.StagedBlocks).Content);
    }

    [Fact]
    public async Task WriteAsyncAndCommitAsync_EveryStagedBlock_AsksTheServiceToVerifyAnMd5Checksum()
    {
        await using var stream = new BlockStagingStream(_blob);

        await stream.WriteAsync(RandomNumberGenerator.GetBytes(BlockSize + 1), TestContext.Current.CancellationToken);
        await stream.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, _blob.StagedBlocks.Count);
        Assert.All(_blob.StagedBlocks, block => Assert.Equal(StorageChecksumAlgorithm.MD5, block.Options.TransferValidation.ChecksumAlgorithm));
    }

    [Fact]
    public async Task CommitAsync_SeveralBlocks_UsesFixedLengthIdsEncodingTheBlockIndex()
    {
        await using var stream = new BlockStagingStream(_blob);

        await stream.WriteAsync(RandomNumberGenerator.GetBytes((2 * BlockSize) + 5), TestContext.Current.CancellationToken);
        await stream.CommitAsync(TestContext.Current.CancellationToken);

        var ids = _blob.StagedBlocks.Select(block => block.BlockId).ToArray();
        Assert.Equal(3, ids.Length);
        Assert.All(ids, id => Assert.Equal(ids[0].Length, id.Length));
        Assert.Equal([0, 1, 2], ids.Select(id => BinaryPrimitives.ReadInt32BigEndian(Convert.FromBase64String(id))));
    }

    [Fact]
    public async Task CommitAsync_PartialLastBlock_StagesTheTailAndCommitsEveryBlockOnceInOrder()
    {
        var data = RandomNumberGenerator.GetBytes(BlockSize + 1234);
        await using var stream = new BlockStagingStream(_blob);
        await stream.WriteAsync(data, TestContext.Current.CancellationToken);

        await stream.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, _blob.StagedBlocks.Count);
        Assert.Equal(1234, _blob.StagedBlocks[1].Content.Length);
        Assert.Equal(data, _blob.StagedBlocks.SelectMany(block => block.Content).ToArray());
        var commit = Assert.Single(_blob.Commits);
        Assert.Equal(_blob.StagedBlocks.Select(block => block.BlockId), commit.BlockIds);
    }

    [Fact]
    public async Task CommitAsync_ExactMultipleOfTheBlockSize_StagesNoEmptyTail()
    {
        await using var stream = new BlockStagingStream(_blob);
        await stream.WriteAsync(RandomNumberGenerator.GetBytes(BlockSize), TestContext.Current.CancellationToken);

        await stream.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Single(_blob.StagedBlocks);
        Assert.Single(Assert.Single(_blob.Commits).BlockIds);
    }

    [Fact]
    public async Task CommitAsync_Blob_IsCreatedOnlyWhenNoneExistsAsOctetStream()
    {
        await using var stream = new BlockStagingStream(_blob);
        await stream.WriteAsync(RandomNumberGenerator.GetBytes(100), TestContext.Current.CancellationToken);

        await stream.CommitAsync(TestContext.Current.CancellationToken);

        var options = Assert.Single(_blob.Commits).Options;
        Assert.Equal(ETag.All, options.Conditions.IfNoneMatch);
        Assert.Equal("application/octet-stream", options.HttpHeaders.ContentType);
    }

    [Theory]
    [InlineData(409, "BlobAlreadyExists")]
    [InlineData(412, "ConditionNotMet")]
    public async Task CommitAsync_RetryMeetsTheBlobItsOwnAttemptCommitted_Succeeds(int status, string errorCode)
    {
        await using var stream = new BlockStagingStream(_blob);
        await stream.WriteAsync(RandomNumberGenerator.GetBytes(BlockSize + 100), TestContext.Current.CancellationToken);
        _blob.CommitFailure = new RequestFailedException(status, "The blob exists.", errorCode, null);
        _blob.CommittedBlockIds = [BlockId(0), BlockId(1)];

        await stream.CommitAsync(TestContext.Current.CancellationToken);

        Assert.False(stream.CanWrite);
    }

    [Fact]
    public async Task CommitAsync_ExistingBlobHoldsOtherBlocks_Throws()
    {
        await using var stream = new BlockStagingStream(_blob);
        await stream.WriteAsync(RandomNumberGenerator.GetBytes(100), TestContext.Current.CancellationToken);
        _blob.CommitFailure = new RequestFailedException(409, "The blob exists.", "BlobAlreadyExists", null);
        _blob.CommittedBlockIds = [BlockId(0), BlockId(1)];

        var exception = await Assert.ThrowsAsync<RequestFailedException>(() => stream.CommitAsync(TestContext.Current.CancellationToken));

        Assert.Equal("BlobAlreadyExists", exception.ErrorCode);
    }

    [Fact]
    public async Task CommitAsync_OtherFailureWhileTheBlocksMatch_Throws()
    {
        await using var stream = new BlockStagingStream(_blob);
        await stream.WriteAsync(RandomNumberGenerator.GetBytes(100), TestContext.Current.CancellationToken);
        _blob.CommitFailure = new RequestFailedException(500, "Server busy.", "ServerBusy", null);
        _blob.CommittedBlockIds = [BlockId(0)];

        var exception = await Assert.ThrowsAsync<RequestFailedException>(() => stream.CommitAsync(TestContext.Current.CancellationToken));

        Assert.Equal("ServerBusy", exception.ErrorCode);
        Assert.True(stream.CanWrite);
    }

    [Fact]
    public async Task FlushAndFlushAsync_BufferedData_StageAndCommitNothing()
    {
        await using var stream = new BlockStagingStream(_blob);
        await stream.WriteAsync(RandomNumberGenerator.GetBytes(100), TestContext.Current.CancellationToken);

        stream.Flush();
        await stream.FlushAsync(TestContext.Current.CancellationToken);

        Assert.Empty(_blob.StagedBlocks);
        Assert.Empty(_blob.Commits);
    }

    [Fact]
    public async Task WriteAsync_AfterCommit_Throws()
    {
        await using var stream = new BlockStagingStream(_blob);
        await stream.WriteAsync(RandomNumberGenerator.GetBytes(100), TestContext.Current.CancellationToken);
        await stream.CommitAsync(TestContext.Current.CancellationToken);

        Assert.False(stream.CanWrite);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await stream.WriteAsync(new byte[1], TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => stream.CommitAsync(TestContext.Current.CancellationToken));
        Assert.Single(_blob.Commits);
    }

    [Fact]
    public async Task DisposeAsync_WithoutCommit_CommitsNothingAndRefusesFurtherWrites()
    {
        var stream = new BlockStagingStream(_blob);
        await stream.WriteAsync(RandomNumberGenerator.GetBytes(100), TestContext.Current.CancellationToken);

        await stream.DisposeAsync();

        Assert.Empty(_blob.Commits);
        Assert.False(stream.CanWrite);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await stream.WriteAsync(new byte[1], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Capabilities_AnyStream_AreWriteOnlyAsynchronousAndForwardOnly()
    {
        await using var stream = new BlockStagingStream(_blob);

        Assert.True(stream.CanWrite);
        Assert.False(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.Throws<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        Assert.Throws<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Position);
    }

    private static string BlockId(int index)
    {
        var bytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, index);

        return Convert.ToBase64String(bytes);
    }
}
