using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.AppConfiguration.Fakes;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Fakes;

internal sealed class RecordingBlockBlobClient : BlockBlobClient
{
    public List<(string BlockId, byte[] Content, BlockBlobStageBlockOptions Options)> StagedBlocks { get; } = [];

    public List<(IReadOnlyList<string> BlockIds, CommitBlockListOptions Options)> Commits { get; } = [];

    public override async Task<Response<BlockInfo>> StageBlockAsync(string base64BlockId, Stream content, BlockBlobStageBlockOptions options, CancellationToken cancellationToken = default)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        StagedBlocks.Add((base64BlockId, copy.ToArray(), options));

        return Response.FromValue(BlobsModelFactory.BlockInfo(null, null, null, null), new FakeResponse());
    }

    public override Task<Response<BlobContentInfo>> CommitBlockListAsync(IEnumerable<string> base64BlockIds, CommitBlockListOptions options, CancellationToken cancellationToken = default)
    {
        Commits.Add(([.. base64BlockIds], options));

        return Task.FromResult(Response.FromValue(BlobsModelFactory.BlobContentInfo(new ETag("\"0x1\""), DateTimeOffset.UnixEpoch, null, null, null, null, 0), new FakeResponse()));
    }
}
