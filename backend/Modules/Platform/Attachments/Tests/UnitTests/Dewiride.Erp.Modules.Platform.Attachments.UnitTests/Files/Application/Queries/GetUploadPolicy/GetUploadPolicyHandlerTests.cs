using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Queries.GetUploadPolicy;

namespace Dewiride.Erp.Modules.Platform.Attachments.UnitTests.Files.Application.Queries.GetUploadPolicy;

public sealed class GetUploadPolicyHandlerTests
{
    [Fact]
    public async Task HandleAsync_Query_ReturnsTheServicesCurrentPolicy()
    {
        var policy = new UploadPolicy(4096, ["application/pdf", "text/plain"]);
        var handler = new GetUploadPolicyHandler(new RecordingAttachmentService { Policy = policy });

        var result = await handler.HandleAsync(new GetUploadPolicyQuery(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(policy, result.Value);
    }
}
