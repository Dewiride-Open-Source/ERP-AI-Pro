using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Endpoints.Security;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Platform.Audit.Changes.Application.Commands.ExportChanges;
using Dewiride.Erp.Modules.Platform.Audit.Changes.Application.Queries.ListChanges;
using Dewiride.Erp.Modules.Platform.Audit.Changes.Endpoints.Export;
using Dewiride.Erp.Modules.Platform.Audit.Changes.Endpoints.Requests;
using Dewiride.Erp.Modules.Platform.Audit.Changes.Endpoints.Responses;
using Dewiride.Erp.Modules.Platform.Audit.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Platform.Audit.Changes.Endpoints;

internal static class ChangeEndpoints
{
    public const string ListRouteName = "Platform.Audit.ListChanges";

    public const string ExportRouteName = "Platform.Audit.ExportChanges";

    public const string ExportConcurrencyPolicy = "platform.audit.exports";

    public const int MaxConcurrentExports = 2;

    public const string NotThisSiteMessage = "The change history is exported only to a page of this site, never to a page, an image or a frame another site opens.";

    public static readonly TimeSpan ExportTimeout = TimeSpan.FromMinutes(5);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/changes", ListAsync)
            .WithName(ListRouteName)
            .WithSummary("Lists the recorded changes, newest first unless the caller sorts them, with the names their actors, records and references have today.")
            .RequirePermission(AuditPermissions.ReadChanges);

        group.MapGet("/changes/export", ExportAsync)
            .WithName(ExportRouteName)
            .WithSummary("Streams the recorded changes the filter selects as a CSV file, oldest first, with one line per changed value.")
            .RequirePermission(AuditPermissions.ExportChanges)
            .RequireRateLimiting(ExportConcurrencyPolicy)
            .Produces<string>(StatusCodes.Status200OK, ChangeCsvWriter.MediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithRequestTimeout(ExportTimeout);
    }

    private static ChangeRecordResponse ToResponse(ChangeRecordDetails details) =>
        new(
            details.Id,
            details.OccurredAt,
            details.Operation,
            details.EntityType,
            details.EntityId,
            details.SubjectType,
            details.SubjectId,
            details.SubjectName,
            details.ActorKind,
            details.ActorId,
            details.ActorName,
            details.ClientAddress,
            details.CorrelationId,
            [.. details.Changes.Select(ToResponse)]);

    private static ChangedValueResponse ToResponse(ChangedValueDetails value) =>
        new(value.Property, value.Kind, value.Before, value.After, value.Reference, value.BeforeName, value.AfterName);

    private static async Task<Results<Ok<PagedResponse<ChangeRecordResponse>>, ProblemHttpResult>> ListAsync(
        [AsParameters] PagingParameters paging,
        IQueryHandler<ListChangesQuery, PagedResult<ChangeRecordDetails>> handler,
        CancellationToken cancellationToken)
    {
        var request = paging.ToListRequest();
        if (request.IsFailure)
        {
            return request.Error!.ToProblem();
        }

        var result = await handler.HandleAsync(new ListChangesQuery(request.Value), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value.ToResponse(ToResponse)) : result.Error!.ToProblem();
    }

    // The file is written once the answer has started, so every refusal comes before it: a request another site made the browser
    // send, a filter outside the list's grammar or fields, an export too large and an export whose record cannot be written. A
    // navigation another site starts carries the SameSite=Lax session cookie, and an export it ran would be recorded as the
    // administrator's own, so only a page of this site and a client that is no browser are served. A failure while the file is
    // written can only end the connection.
    private static async Task<Results<PushStreamHttpResult, ProblemHttpResult>> ExportAsync(
        [AsParameters] ExportChangesRequest request,
        HttpContext httpContext,
        ICommandHandler<ExportChangesCommand, ChangeExport> handler,
        CancellationToken cancellationToken)
    {
        if (!FetchMetadata.IsFromThisOrigin(httpContext.Request.Headers))
        {
            return Error.Validation(ProblemTypes.RequestInvalid, NotThisSiteMessage).ToProblem();
        }

        var filter = FilterRequest.Parse(request.Filter);
        if (filter.IsFailure)
        {
            return filter.Error!.ToProblem();
        }

        var result = await handler.HandleAsync(new ExportChangesCommand(filter.Value), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!.ToProblem();
        }

        var export = result.Value;

        return TypedResults.Stream(body => ChangeCsvWriter.WriteAsync(body, export, cancellationToken), ChangeCsvWriter.ContentType, ChangeCsvWriter.FileNameOf(export));
    }
}
