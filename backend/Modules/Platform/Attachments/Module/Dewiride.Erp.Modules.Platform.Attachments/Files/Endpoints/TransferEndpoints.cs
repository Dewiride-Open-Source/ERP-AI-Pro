using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Commands.CreateDownloadLink;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Commands.UploadAttachment;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Queries.OpenAttachmentContent;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Requests;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Responses;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Uploads;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints;

internal static class TransferEndpoints
{
    public const string TimeoutPolicy = "platform.attachments.transfer";

    public const string DownloadRouteName = "Platform.Attachments.Download";

    private const string FormData = "multipart/form-data";

    public static void Map(RouteGroupBuilder group)
    {
        var upload = group.MapPost("/", UploadAsync)
            .WithName("Platform.Attachments.Upload")
            .WithSummary("Stores one file sent as a multipart/form-data part with a file name.")
            .WithRequestTimeout(TimeoutPolicy)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                operation.RequestBody = new OpenApiRequestBody
                {
                    Required = true,
                    Description = "The file, as the single part of a multipart/form-data body that carries a file name.",
                    Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
                    {
                        [FormData] = new()
                        {
                            Schema = new OpenApiSchema
                            {
                                Type = JsonSchemaType.Object,
                                Required = new HashSet<string>(StringComparer.Ordinal) { "file" },
                                Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                                {
                                    ["file"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
                                },
                            },
                        },
                    },
                };

                return Task.CompletedTask;
            })
            .AllowAnonymous();
        upload.Add(endpoint => endpoint.Metadata.Add(new UploadSizeLimit(endpoint.ApplicationServices.GetRequiredService<IOptionsMonitor<AttachmentsOptions>>())));

        group.MapPost("/{id:guid}/download-links", CreateDownloadLinkAsync)
            .WithName("Platform.Attachments.CreateDownloadLink")
            .WithSummary("Creates a short-lived link that downloads the attachment for the person asking.")
            .AllowAnonymous();

        group.MapGet("/{id:guid}/content", OpenContentAsync)
            .WithName(DownloadRouteName)
            .WithSummary("Streams the original file for a valid, unexpired link of the person asking.")
            .Produces<Stream>(StatusCodes.Status200OK, "application/octet-stream")
            .WithRequestTimeout(TimeoutPolicy)
            .AllowAnonymous();
    }

    // Any IOException other than a BadHttpRequestException (the size limit or a malformed body, which the exception handler
    // answers) comes from reading a request body that ended early, as when a proxy cuts an upload short.
    private static async Task<Results<Created<AttachmentResponse>, ProblemHttpResult>> UploadAsync(
        HttpRequest request,
        LinkGenerator links,
        ICommandHandler<UploadAttachmentCommand, AttachmentDetails> handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var file = await MultipartFileReader.ReadFirstFileAsync(request, cancellationToken);
            if (file.IsFailure)
            {
                return file.Error!.ToProblem();
            }

            var result = await handler.HandleAsync(new UploadAttachmentCommand(file.Value.FileName, file.Value.ContentType, file.Value.Content), cancellationToken);

            return result.IsSuccess
                ? TypedResults.Created(PathTo(links, request.HttpContext, AttachmentEndpoints.GetRouteName, new { id = result.Value.Id.Value }), AttachmentEndpoints.ToResponse(result.Value))
                : result.Error!.ToProblem();
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException and not BadHttpRequestException && !cancellationToken.IsCancellationRequested)
        {
            return AttachmentErrors.Incomplete.ToProblem();
        }
    }

    private static async Task<Results<Ok<DownloadLinkResponse>, ProblemHttpResult>> CreateDownloadLinkAsync(
        Guid id,
        HttpContext httpContext,
        LinkGenerator links,
        ICommandHandler<CreateDownloadLinkCommand, DownloadLinkDetails> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new CreateDownloadLinkCommand(AttachmentId.From(id)), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!.ToProblem();
        }

        var url = PathTo(links, httpContext, DownloadRouteName, new { id, link = result.Value.Token });

        return TypedResults.Ok(new DownloadLinkResponse(url, result.Value.ExpiresAt));
    }

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> OpenContentAsync(
        Guid id,
        [AsParameters] OpenContentRequest link,
        HttpResponse response,
        IQueryHandler<OpenAttachmentContentQuery, AttachmentDownload> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new OpenAttachmentContentQuery(AttachmentId.From(id), link.Link), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!.ToProblem();
        }

        // The decrypting stream cannot seek, so the result would send no length of its own; the recorded size lets a browser
        // show progress and recognise a download that broke off.
        response.ContentLength = result.Value.SizeBytes;

        return TypedResults.Stream(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    private static string PathTo(LinkGenerator links, HttpContext httpContext, string routeName, object values) =>
        links.GetPathByName(httpContext, routeName, values)
        ?? throw new InvalidOperationException($"No endpoint is named '{routeName}'.");
}
