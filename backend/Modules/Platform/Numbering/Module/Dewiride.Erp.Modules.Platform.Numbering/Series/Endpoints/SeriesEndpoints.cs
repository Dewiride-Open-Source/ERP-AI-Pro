using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Idempotency.Http;
using Dewiride.Erp.BuildingBlocks.Numbering.Administration;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;
using Dewiride.Erp.Modules.Platform.Numbering.Contracts;
using Dewiride.Erp.Modules.Platform.Numbering.Series.Application.Commands.ChangeNumberSeries;
using Dewiride.Erp.Modules.Platform.Numbering.Series.Application.Commands.SetUpNumberSeries;
using Dewiride.Erp.Modules.Platform.Numbering.Series.Application.Queries.GetNumberSeries;
using Dewiride.Erp.Modules.Platform.Numbering.Series.Application.Queries.ListNumberedDocuments;
using Dewiride.Erp.Modules.Platform.Numbering.Series.Endpoints.Requests;
using Dewiride.Erp.Modules.Platform.Numbering.Series.Endpoints.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Platform.Numbering.Series.Endpoints;

// A change names the version it was made from, so it takes no idempotency key: repeated, it finds the numbering at a newer version
// and is refused.
internal static class SeriesEndpoints
{
    public const string ListDocumentsRouteName = "Platform.Numbering.ListNumberedDocuments";

    public const string GetSeriesRouteName = "Platform.Numbering.GetNumberSeries";

    public const string SetUpSeriesRouteName = "Platform.Numbering.SetUpNumberSeries";

    public const string ChangeSeriesRouteName = "Platform.Numbering.ChangeNumberSeries";

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/documents", ListDocumentsAsync)
            .WithName(ListDocumentsRouteName)
            .WithSummary("Lists the kinds of document the ERP numbers, by title, each with the rule its numbers follow this financial year and its numbering once it is set up.")
            .RequirePermission(NumberingPermissions.Read);

        group.MapGet("/series/{id:guid}", GetSeriesAsync)
            .WithName(GetSeriesRouteName)
            .WithSummary("Describes one numbering with the kind of document it numbers and the financial years it has opened.")
            .RequirePermission(NumberingPermissions.Read);

        group.MapPost("/series", SetUpAsync)
            .WithName(SetUpSeriesRouteName)
            .WithSummary("Sets up the numbering of a kind of document, opening the current financial year at its next number.")
            .RequirePermission(NumberingPermissions.Change)
            .RequireIdempotencyKey()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPut("/series/{id:guid}", ChangeAsync)
            .WithName(ChangeSeriesRouteName)
            .WithSummary("Changes a numbering: the current financial year takes the new format and next number while it has given no number, and otherwise the format applies from the next year.")
            .RequirePermission(NumberingPermissions.Change)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static NumberFormat ToFormat(NumberFormatRequest request) => new(request.Prefix!, request.Year!.Value, request.Separator!, request.Digits!.Value);

    private static NumberFormatResponse ToResponse(NumberFormat format) => new(format.Prefix, format.Year, format.Separator, format.Digits);

    private static NumberedDocumentResponse ToResponse(NumberedDocumentDetails details) =>
        new(
            details.Document.Key,
            details.Document.Title,
            details.Document.Description,
            details.CurrentYear.Label,
            details.Rule is { } rule ? new SerialNumberRuleResponse(rule.MaxLength, rule.SpecialCharacters) : null,
            details.Series is { } series ? ToResponse(series) : null);

    private static NumberSeriesResponse ToResponse(NumberSeriesDetails details) =>
        new(details.Id.Value, ToResponse(details.Format), details.Version, [.. details.Years.Select(ToResponse)]);

    private static NumberSeriesYearResponse ToResponse(NumberSeriesYearDetails details) =>
        new(
            details.Year.Label,
            ToResponse(details.Format),
            details.MaxLength,
            details.FirstNumber,
            details.NextNumber,
            details.LastNumber,
            details.InUse,
            details.FirstText,
            details.NextText,
            details.LastText,
            details.LastIssuedText);

    private static async Task<Results<Ok<IReadOnlyList<NumberedDocumentResponse>>, ProblemHttpResult>> ListDocumentsAsync(
        IQueryHandler<ListNumberedDocumentsQuery, IReadOnlyList<NumberedDocumentDetails>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListNumberedDocumentsQuery(), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!.ToProblem();
        }

        IReadOnlyList<NumberedDocumentResponse> documents = [.. result.Value.Select(ToResponse)];

        return TypedResults.Ok(documents);
    }

    private static async Task<Results<Ok<NumberedDocumentResponse>, ProblemHttpResult>> GetSeriesAsync(
        Guid id,
        IQueryHandler<GetNumberSeriesQuery, NumberedDocumentDetails> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetNumberSeriesQuery(NumberSeriesId.From(id)), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(ToResponse(result.Value)) : result.Error!.ToProblem();
    }

    private static async Task<Results<Created<NumberedDocumentResponse>, ProblemHttpResult>> SetUpAsync(
        SetUpNumberSeriesRequest request,
        ICommandHandler<SetUpNumberSeriesCommand, NumberedDocumentDetails> handler,
        LinkGenerator links,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new SetUpNumberSeriesCommand(request.DocumentType!, ToFormat(request.Format!), request.NextNumber!.Value), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!.ToProblem();
        }

        var location = links.GetPathByName(httpContext, GetSeriesRouteName, new { id = result.Value.Series!.Id.Value })
            ?? throw new InvalidOperationException($"No endpoint is named '{GetSeriesRouteName}'.");

        return TypedResults.Created(location, ToResponse(result.Value));
    }

    private static async Task<Results<Ok<NumberedDocumentResponse>, ProblemHttpResult>> ChangeAsync(
        Guid id,
        ChangeNumberSeriesRequest request,
        ICommandHandler<ChangeNumberSeriesCommand, NumberedDocumentDetails> handler,
        CancellationToken cancellationToken)
    {
        var command = new ChangeNumberSeriesCommand(NumberSeriesId.From(id), ToFormat(request.Format!), request.NextNumber, request.Version!);
        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(ToResponse(result.Value)) : result.Error!.ToProblem();
    }
}
