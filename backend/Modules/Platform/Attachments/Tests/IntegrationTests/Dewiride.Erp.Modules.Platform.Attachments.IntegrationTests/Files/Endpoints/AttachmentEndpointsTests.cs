using System.Net;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.Modules.Platform.Attachments.Contracts.Files;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Responses;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authorization;

namespace Dewiride.Erp.Modules.Platform.Attachments.IntegrationTests.Files.Endpoints;

public sealed class AttachmentEndpointsTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Theory]
    [InlineData("GET", "/api/platform/attachments", AttachmentsPermissions.Read)]
    [InlineData("GET", "/api/platform/attachments/policy", AttachmentsPermissions.Read)]
    [InlineData("POST", "/api/platform/attachments", AttachmentsPermissions.Upload)]
    [InlineData("GET", "/api/platform/attachments/{id:guid}", AttachmentsPermissions.Read)]
    [InlineData("DELETE", "/api/platform/attachments/{id:guid}", AttachmentsPermissions.Delete)]
    [InlineData("POST", "/api/platform/attachments/{id:guid}/download-links", AttachmentsPermissions.Read)]
    [InlineData("GET", "/api/platform/attachments/{id:guid}/content", AttachmentsPermissions.Read)]
    public void Route_OfEachUseCase_RequiresItsOwnPermission(string method, string route, string permission)
    {
        Assert.Equal(permission, RoutePermissions.Of(factory.Services, method, route));
    }

    [Fact]
    public async Task Get_UploadedAttachment_ReturnsItsDetails()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);
        var uploaded = await AttachmentsApi.UploadAsync(client, SampleFiles.OnePixelPng(), SampleFiles.PngType, $"pixel-{AttachmentsApi.UniqueToken()}.png");

        using var response = await client.GetAsync(AttachmentsApi.Path($"/{uploaded.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(uploaded, await response.Content.ReadFromJsonAsync<AttachmentResponse>(AttachmentsApi.Json, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Get_UnknownId_AnswersNotFound()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await client.GetAsync(AttachmentsApi.Path($"/{Guid.CreateVersion7()}"), TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, AttachmentErrors.NotFound.Code);
    }

    [Fact]
    public async Task Delete_UploadedAttachment_AnswersNoContentAndRemovesItFromGetAndList()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var token = AttachmentsApi.UniqueToken();
        var uploaded = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"to be deleted {token}"), SampleFiles.TextType, $"{token}-deleted.txt");

        using var response = await client.DeleteAsync(AttachmentsApi.Path($"/{uploaded.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var get = await client.GetAsync(AttachmentsApi.Path($"/{uploaded.Id}"), TestContext.Current.CancellationToken);
        await AttachmentsApi.AssertProblemAsync(get, HttpStatusCode.NotFound, AttachmentErrors.NotFound.Code);
        var list = await client.GetFromJsonAsync<PagedResponse<AttachmentResponse>>(
            AttachmentsApi.Path($"?filter={Uri.EscapeDataString($"fileName:contains:{token}")}"),
            AttachmentsApi.Json,
            TestContext.Current.CancellationToken);
        Assert.NotNull(list);
        Assert.Empty(list.Items);
        Assert.Equal(0, list.TotalCount);
    }

    [Fact]
    public async Task Delete_AttachmentDeletedBefore_AnswersNotFound()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var uploaded = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"deleted twice {AttachmentsApi.UniqueToken()}"), SampleFiles.TextType, "twice.txt");
        using var first = await client.DeleteAsync(AttachmentsApi.Path($"/{uploaded.Id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        using var response = await client.DeleteAsync(AttachmentsApi.Path($"/{uploaded.Id}"), TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, AttachmentErrors.NotFound.Code);
    }

    [Fact]
    public async Task Delete_AsAnEmployee_AnswersForbiddenAndKeepsTheAttachment()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);
        var uploaded = await AttachmentsApi.UploadAsync(client, SampleFiles.Text($"kept {AttachmentsApi.UniqueToken()}"), SampleFiles.TextType, "kept.txt");

        using var response = await client.DeleteAsync(AttachmentsApi.Path($"/{uploaded.Id}"), TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.Forbidden, "request.forbidden");
        using var get = await client.GetAsync(AttachmentsApi.Path($"/{uploaded.Id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownId_AnswersNotFound()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await client.DeleteAsync(AttachmentsApi.Path($"/{Guid.CreateVersion7()}"), TestContext.Current.CancellationToken);

        await AttachmentsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, AttachmentErrors.NotFound.Code);
    }
}
