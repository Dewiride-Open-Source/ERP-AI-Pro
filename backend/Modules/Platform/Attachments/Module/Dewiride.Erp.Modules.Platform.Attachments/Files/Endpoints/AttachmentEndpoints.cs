using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Service;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Commands.DeleteAttachment;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Queries.GetAttachment;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Queries.GetUploadPolicy;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Application.Queries.ListAttachments;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints;

internal static class AttachmentEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync)
            .WithName("Platform.Attachments.List")
            .WithSummary("Lists stored attachments, newest first unless the caller sorts them.")
            .AllowAnonymous();

        group.MapGet("/policy", GetPolicyAsync)
            .WithName("Platform.Attachments.GetUploadPolicy")
            .WithSummary("Describes the largest file and the media types an upload may have.")
            .AllowAnonymous();

        group.MapGet("/{id:guid}", GetAsync)
            .WithName("Platform.Attachments.Get")
            .WithSummary("Describes one stored attachment.")
            .AllowAnonymous();

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("Platform.Attachments.Delete")
            .WithSummary("Deletes an attachment; its download links stop working.")
            .AllowAnonymous();
    }

    public static AttachmentResponse ToResponse(AttachmentDetails details) =>
        new(details.Id.Value, details.FileName, details.ContentType, details.SizeBytes, details.Sha256, details.ScanStatus, details.CreatedAt, details.CreatedBy);

    private static async Task<Results<Ok<PagedResponse<AttachmentResponse>>, ProblemHttpResult>> ListAsync(
        [AsParameters] PagingParameters paging,
        IQueryHandler<ListAttachmentsQuery, PagedResult<AttachmentDetails>> handler,
        CancellationToken cancellationToken)
    {
        var request = paging.ToListRequest();
        if (request.IsFailure)
        {
            return request.Error!.ToProblem();
        }

        var result = await handler.HandleAsync(new ListAttachmentsQuery(request.Value), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value.ToResponse(ToResponse)) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<UploadPolicyResponse>, ProblemHttpResult>> GetPolicyAsync(
        IQueryHandler<GetUploadPolicyQuery, UploadPolicy> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetUploadPolicyQuery(), cancellationToken);

        return result.IsSuccess
            ? TypedResults.Ok(new UploadPolicyResponse(result.Value.MaxSizeBytes, result.Value.AllowedContentTypes))
            : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<AttachmentResponse>, ProblemHttpResult>> GetAsync(
        Guid id,
        IQueryHandler<GetAttachmentQuery, AttachmentDetails> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetAttachmentQuery(AttachmentId.From(id)), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(ToResponse(result.Value)) : result.Error!.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid id,
        ICommandHandler<DeleteAttachmentCommand, AttachmentId> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteAttachmentCommand(AttachmentId.From(id)), cancellationToken);

        return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
    }
}
