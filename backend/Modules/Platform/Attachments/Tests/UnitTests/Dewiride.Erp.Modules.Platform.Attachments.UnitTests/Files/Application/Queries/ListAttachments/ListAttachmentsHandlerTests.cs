using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Queries.ListAttachments;

namespace Dewiride.Erp.Modules.Platform.Attachments.UnitTests.Files.Application.Queries.ListAttachments;

public sealed class ListAttachmentsHandlerTests
{
    [Fact]
    public async Task HandleAsync_ListRequest_HandsItToTheServiceAndReturnsThePage()
    {
        var service = new RecordingAttachmentService();
        var request = ListRequest.Parse(2, 10, "fileName:asc", "contentType:eq:application%2Fpdf").Value;
        var handler = new ListAttachmentsHandler(service);

        var result = await handler.HandleAsync(new ListAttachmentsQuery(request), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(RecordingAttachmentService.SampleDetails, Assert.Single(result.Value.Items));
        Assert.Same(request, service.ReceivedRequest);
        Assert.Equal(TestContext.Current.CancellationToken, service.ReceivedCancellation);
    }

    [Fact]
    public async Task HandleAsync_ServiceRejectsTheRequest_ReturnsTheQueryError()
    {
        var error = QueryErrors.Field("owner", ["fileName", "contentType", "sizeBytes", "createdAt"]);
        var handler = new ListAttachmentsHandler(new RecordingAttachmentService { Page = error });

        var result = await handler.HandleAsync(new ListAttachmentsQuery(ListRequest.Default), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }
}
