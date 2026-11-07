using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Idempotency.Http;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Commands.CorrectExchangeRate;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Commands.ImportExchangeRates;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Commands.RecordExchangeRate;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Commands.RemoveExchangeRate;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Queries.GetExchangeRate;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Queries.ListExchangeRates;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Endpoints.Requests;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Endpoints.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Endpoints;

internal static class ExchangeRateEndpoints
{
    public const string GetRouteName = "Platform.ReferenceData.GetExchangeRate";

    public static void Map(RouteGroupBuilder group)
    {
        var routes = group.MapGroup("/exchange-rates");

        routes.MapGet("/", ListAsync)
            .WithName("Platform.ReferenceData.ListExchangeRates")
            .WithSummary("Lists the exchange rates, the newest day first unless the caller sorts them.")
            .RequirePermission(ReferenceDataPermissions.ReadExchangeRates);

        routes.MapGet("/{id:guid}", GetAsync)
            .WithName(GetRouteName)
            .WithSummary("Describes one exchange rate.")
            .RequirePermission(ReferenceDataPermissions.ReadExchangeRates);

        routes.MapPost("/", RecordAsync)
            .WithName("Platform.ReferenceData.RecordExchangeRate")
            .WithSummary("Records the rupee value of a foreign currency for one day.")
            .RequirePermission(ReferenceDataPermissions.RecordExchangeRates)
            .RequireIdempotencyKey()
            .ProducesProblem(StatusCodes.Status409Conflict);

        routes.MapPost("/imports", ImportAsync)
            .WithName("Platform.ReferenceData.ImportExchangeRates")
            .WithSummary("Records the rates of a file all together, leaving alone those already recorded with the same rate, or none of them when a rate cannot be recorded.")
            .RequirePermission(ReferenceDataPermissions.RecordExchangeRates)
            .RequireIdempotencyKey()
            .ProducesProblem(StatusCodes.Status409Conflict);

        routes.MapPut("/{id:guid}", CorrectAsync)
            .WithName("Platform.ReferenceData.CorrectExchangeRate")
            .WithSummary("Corrects the rate, unit and source of an exchange rate.")
            .RequirePermission(ReferenceDataPermissions.CorrectExchangeRates)
            .ProducesProblem(StatusCodes.Status409Conflict);

        routes.MapDelete("/{id:guid}", RemoveAsync)
            .WithName("Platform.ReferenceData.RemoveExchangeRate")
            .WithSummary("Removes an exchange rate recorded by mistake; its currency and day can then be recorded again.")
            .RequirePermission(ReferenceDataPermissions.CorrectExchangeRates);
    }

    private static ExchangeRateResponse ToResponse(ExchangeRateDetails details) =>
        new(details.Id, details.Currency.Code, details.RateDate, details.Rate, details.Unit, details.Source, details.Version);

    private static async Task<Results<Ok<PagedResponse<ExchangeRateResponse>>, ProblemHttpResult>> ListAsync(
        [AsParameters] PagingParameters paging,
        IQueryHandler<ListExchangeRatesQuery, PagedResult<ExchangeRateDetails>> handler,
        CancellationToken cancellationToken)
    {
        var request = paging.ToListRequest();
        if (request.IsFailure)
        {
            return request.Error!.ToProblem();
        }

        var result = await handler.HandleAsync(new ListExchangeRatesQuery(request.Value), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value.ToResponse(ToResponse)) : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<ExchangeRateResponse>, ProblemHttpResult>> GetAsync(
        Guid id,
        IQueryHandler<GetExchangeRateQuery, ExchangeRateDetails> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetExchangeRateQuery(id), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(ToResponse(result.Value)) : result.Error!.ToProblem();
    }

    private static async Task<Results<Created<ExchangeRateResponse>, ProblemHttpResult>> RecordAsync(
        RecordExchangeRateRequest request,
        ICommandHandler<RecordExchangeRateCommand, Guid> handler,
        IQueryHandler<GetExchangeRateQuery, ExchangeRateDetails> records,
        LinkGenerator links,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new RecordExchangeRateCommand(request.Currency!, request.RateDate!.Value, request.Rate!.Value, request.Unit!.Value, request.Source);
        var recorded = await handler.HandleAsync(command, cancellationToken);
        if (recorded.IsFailure)
        {
            return recorded.Error!.ToProblem();
        }

        var saved = await records.HandleAsync(new GetExchangeRateQuery(recorded.Value), cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error!.ToProblem();
        }

        var location = links.GetPathByName(httpContext, GetRouteName, new { id = recorded.Value })
            ?? throw new InvalidOperationException($"No endpoint is named '{GetRouteName}'.");

        return TypedResults.Created(location, ToResponse(saved.Value));
    }

    private static async Task<Results<Ok<ImportedExchangeRatesResponse>, ProblemHttpResult>> ImportAsync(
        ImportExchangeRatesRequest request,
        ICommandHandler<ImportExchangeRatesCommand, ImportedExchangeRates> handler,
        CancellationToken cancellationToken)
    {
        var rates = request.Rates!
            .Select(rate => new ImportedExchangeRate(rate.Currency!, rate.RateDate!.Value, rate.Rate!.Value, rate.Unit!.Value, rate.Source))
            .ToList();
        var result = await handler.HandleAsync(new ImportExchangeRatesCommand(rates), cancellationToken);

        return result.IsSuccess
            ? TypedResults.Ok(new ImportedExchangeRatesResponse(result.Value.Recorded, result.Value.Unchanged))
            : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<ExchangeRateResponse>, ProblemHttpResult>> CorrectAsync(
        Guid id,
        CorrectExchangeRateRequest request,
        ICommandHandler<CorrectExchangeRateCommand, Guid> handler,
        IQueryHandler<GetExchangeRateQuery, ExchangeRateDetails> records,
        CancellationToken cancellationToken)
    {
        var corrected = await handler.HandleAsync(new CorrectExchangeRateCommand(id, request.Rate!.Value, request.Unit!.Value, request.Source, request.Version!), cancellationToken);
        if (corrected.IsFailure)
        {
            return corrected.Error!.ToProblem();
        }

        var saved = await records.HandleAsync(new GetExchangeRateQuery(corrected.Value), cancellationToken);

        return saved.IsSuccess ? TypedResults.Ok(ToResponse(saved.Value)) : saved.Error!.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveAsync(
        Guid id,
        ICommandHandler<RemoveExchangeRateCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new RemoveExchangeRateCommand(id), cancellationToken);

        return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
    }
}
