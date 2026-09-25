using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.Errors;

public sealed class ProblemDetailsCustomizerTests
{
    private const string CorrelationValue = "0199a1b2c3d4e5f60718293a4b5c6d7e";

    [Fact]
    public void Customize_ProblemCarryingACode_TakesTheTypeFromThatCode()
    {
        var context = Context(new ProblemDetails { Status = StatusCodes.Status409Conflict, Extensions = { [ResultExtensions.CodeExtension] = "invoice.already-issued" } });

        ProblemDetailsCustomizer.Customize(context);

        Assert.Equal("/problems/invoice.already-issued", context.ProblemDetails.Type);
        Assert.Equal("invoice.already-issued", context.ProblemDetails.Extensions[ResultExtensions.CodeExtension]);
    }

    [Fact]
    public void Customize_ProblemWithoutACode_AddsTheCodeOfItsStatusAndTheMatchingType()
    {
        var context = Context(new ProblemDetails { Status = StatusCodes.Status404NotFound });

        ProblemDetailsCustomizer.Customize(context);

        Assert.Equal(ProblemTypes.ResourceNotFound, context.ProblemDetails.Extensions[ResultExtensions.CodeExtension]);
        Assert.Equal("/problems/resource.not-found", context.ProblemDetails.Type);
    }

    [Fact]
    public void Customize_ProblemWithoutAStatus_TakesTheStatusOfTheResponse()
    {
        var context = Context(new ProblemDetails());
        context.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        ProblemDetailsCustomizer.Customize(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.ProblemDetails.Status);
        Assert.Equal("/problems/request.invalid", context.ProblemDetails.Type);
    }

    [Fact]
    public void Customize_ProblemWithoutAnInstance_TakesTheRequestPath()
    {
        var context = Context(new ProblemDetails { Status = StatusCodes.Status404NotFound });
        context.HttpContext.Request.Path = "/api/platform/system-info/startups";

        ProblemDetailsCustomizer.Customize(context);

        Assert.Equal("/api/platform/system-info/startups", context.ProblemDetails.Instance);
    }

    [Fact]
    public void Customize_ProblemCarryingAnInstance_KeepsIt()
    {
        var context = Context(new ProblemDetails { Status = StatusCodes.Status404NotFound, Instance = "/api/finance/sales/invoices/1" });
        context.HttpContext.Request.Path = "/api/platform/system-info";

        ProblemDetailsCustomizer.Customize(context);

        Assert.Equal("/api/finance/sales/invoices/1", context.ProblemDetails.Instance);
    }

    [Fact]
    public void Customize_RequestCarryingACorrelationId_ReportsItAsTheTraceId()
    {
        var context = Context(new ProblemDetails { Status = StatusCodes.Status404NotFound });
        context.HttpContext.Features.Set<ICorrelationIdFeature>(new CorrelationIdFeature(CorrelationValue));

        ProblemDetailsCustomizer.Customize(context);

        Assert.Equal(CorrelationValue, context.ProblemDetails.Extensions[ProblemDetailsCustomizer.TraceIdExtension]);
    }

    [Fact]
    public void Customize_RequestWithoutACorrelationId_ReportsTheTraceIdentifier()
    {
        var context = Context(new ProblemDetails { Status = StatusCodes.Status404NotFound });
        context.HttpContext.TraceIdentifier = "0HN7A3B4C5D6E";

        ProblemDetailsCustomizer.Customize(context);

        Assert.Equal("0HN7A3B4C5D6E", context.ProblemDetails.Extensions[ProblemDetailsCustomizer.TraceIdExtension]);
    }

    [Fact]
    public void Customize_ValidationProblem_KeepsTheFieldErrorsAndTheValidationCode()
    {
        var problem = new HttpValidationProblemDetails(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["take"] = ["must be between 1 and 100"] })
        {
            Status = StatusCodes.Status400BadRequest,
        };

        var context = Context(problem);
        ProblemDetailsCustomizer.Customize(context);

        Assert.Equal("/problems/request.invalid", problem.Type);
        Assert.Equal(["must be between 1 and 100"], problem.Errors["take"]);
    }

    private static ProblemDetailsContext Context(ProblemDetails problem) =>
        new() { HttpContext = new DefaultHttpContext(), ProblemDetails = problem };
}
