using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Modules.Platform.Attachments.IntegrationTests.Files.Endpoints;

internal static class AttachmentsApi
{
    public const string Route = "/api/platform/attachments";

    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static Uri Path(string suffix = "") => new(Route + suffix, UriKind.Relative);

    public static string UniqueToken() => Guid.CreateVersion7().ToString("N")[^12..];

    public static MultipartFormDataContent FileContent(byte[] bytes, string? contentType, string fileName)
    {
        var file = new ByteArrayContent(bytes);
        if (contentType is not null)
        {
            file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        }

        var content = new MultipartFormDataContent();
        content.Add(file, "file", fileName);

        return content;
    }

    public static async Task<HttpResponseMessage> PostFileAsync(HttpClient client, byte[] bytes, string? contentType, string fileName)
    {
        using var content = FileContent(bytes, contentType, fileName);

        return await client.PostAsync(Path(), content, TestContext.Current.CancellationToken);
    }

    public static async Task<AttachmentResponse> UploadAsync(HttpClient client, byte[] bytes, string contentType, string fileName)
    {
        using var response = await PostFileAsync(client, bytes, contentType, fileName);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var attachment = await response.Content.ReadFromJsonAsync<AttachmentResponse>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(attachment);

        return attachment;
    }

    public static async Task<DownloadLinkResponse> CreateDownloadLinkAsync(HttpClient client, Guid id)
    {
        using var response = await client.PostAsync(Path($"/{id}/download-links"), content: null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var link = await response.Content.ReadFromJsonAsync<DownloadLinkResponse>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(link);

        return link;
    }

    public static async Task<ProblemDetails> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal((int)status, problem.Status);
        Assert.Equal($"/problems/{code}", problem.Type);
        Assert.Equal(code, problem.Extensions[ResultExtensions.CodeExtension]?.ToString());
        Assert.Equal(response.RequestMessage?.RequestUri?.AbsolutePath, problem.Instance);

        return problem;
    }
}
