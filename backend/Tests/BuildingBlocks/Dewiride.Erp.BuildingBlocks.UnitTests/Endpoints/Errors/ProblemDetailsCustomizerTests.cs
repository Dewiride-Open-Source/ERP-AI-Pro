using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.Errors;

public sealed class ProblemDetailsCustomizerTests
{
    private const string CorrelationValue = "0199a1b2c3d4e5f60718293a4b5c6d7e";

    private const int ManyKeys = 100_000;

    private const int ManyKeysTimeoutMilliseconds = 5_000;

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

        Assert.Equal(CorrelationValue, context.ProblemDetails.Extensions[ProblemTypes.TraceIdExtension]);
    }

    [Fact]
    public void Customize_RequestWithoutACorrelationId_ReportsTheTraceIdentifier()
    {
        var context = Context(new ProblemDetails { Status = StatusCodes.Status404NotFound });
        context.HttpContext.TraceIdentifier = "0HN7A3B4C5D6E";

        ProblemDetailsCustomizer.Customize(context);

        Assert.Equal("0HN7A3B4C5D6E", context.ProblemDetails.Extensions[ProblemTypes.TraceIdExtension]);
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

    [Theory]
    [InlineData("Customer.ShippingAddress.Street", "customer.shippingAddress.street")]
    [InlineData("OrderItems[0].Description", "orderItems[0].description")]
    [InlineData("Lines[12].TaxLines[3].Rate", "lines[12].taxLines[3].rate")]
    [InlineData("GSTIN", "gstin")]
    [InlineData("IFSCCode", "ifscCode")]
    [InlineData("BankAccount.IFSCCode", "bankAccount.ifscCode")]
    [InlineData("Supplier.PAN", "supplier.pan")]
    [InlineData("IDs", "iDs")]
    [InlineData("", "")]
    [InlineData("take", "take")]
    [InlineData("customer.shippingAddress.street", "customer.shippingAddress.street")]
    [InlineData("orderItems[0].description", "orderItems[0].description")]
    public void Customize_ValidationProblem_CamelCasesEveryMemberNameOfTheKey(string key, string expected)
    {
        var problem = ValidationProblem(new(StringComparer.Ordinal) { [key] = ["message"] });

        ProblemDetailsCustomizer.Customize(Context(problem));

        var field = Assert.Single(problem.Errors);
        Assert.Equal(expected, field.Key);
        Assert.Equal(["message"], field.Value);
    }

    [Fact]
    public void Customize_ValidationProblemWithSeveralKeys_KeepsEachKeyWithItsMessages()
    {
        var problem = ValidationProblem(new(StringComparer.Ordinal)
        {
            ["Customer.ShippingAddress.Street"] = ["Enter the street."],
            ["OrderItems[0].Description"] = ["Enter a description.", "Use 200 characters or fewer."],
            [""] = ["The order is empty."],
        });

        ProblemDetailsCustomizer.Customize(Context(problem));

        Assert.Equal(3, problem.Errors.Count);
        Assert.Equal(["Enter the street."], problem.Errors["customer.shippingAddress.street"]);
        Assert.Equal(["Enter a description.", "Use 200 characters or fewer."], problem.Errors["orderItems[0].description"]);
        Assert.Equal(["The order is empty."], problem.Errors[""]);
    }

    [Fact]
    public void Customize_ValidationProblemWithKeysThatMeetAfterConversion_KeepsEveryMessageUnderOneKey()
    {
        var problem = ValidationProblem(new(StringComparer.Ordinal)
        {
            ["Customer.Name"] = ["Enter the name."],
            ["customer.name"] = ["The name is already registered."],
        });

        ProblemDetailsCustomizer.Customize(Context(problem));

        var field = Assert.Single(problem.Errors);
        Assert.Equal("customer.name", field.Key);
        Assert.Equal(["Enter the name.", "The name is already registered."], field.Value.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Customize_ValidationProblem_KeepsTheKeysInTheOrderValidationReportedThem()
    {
        var problem = ValidationProblem(new(StringComparer.Ordinal)
        {
            ["OrderItems[1].Description"] = ["Enter a description."],
            ["gstin"] = ["Enter a GSTIN."],
            [""] = ["The order is empty."],
            ["Customer.Name"] = ["Enter the name."],
        });

        ProblemDetailsCustomizer.Customize(Context(problem));

        Assert.Equal(["orderItems[1].description", "gstin", "", "customer.name"], problem.Errors.Keys);
    }

    [Fact]
    public void Customize_ValidationProblemWhoseKeysFollowTheGrammar_KeepsItsErrors()
    {
        var problem = ValidationProblem(new(StringComparer.Ordinal)
        {
            ["take"] = ["must be between 1 and 100"],
            ["orderItems[0].description"] = ["Enter a description."],
            [""] = ["The order is empty."],
        });
        var errors = problem.Errors;

        ProblemDetailsCustomizer.Customize(Context(problem));

        Assert.Same(errors, problem.Errors);
    }

#pragma warning disable xUnit1069 // Customize is synchronous and takes no token; the timeout exists to fail the test when the conversion outgrows linear time.
    [Fact(Timeout = ManyKeysTimeoutMilliseconds)]
#pragma warning restore xUnit1069
    public void Customize_ValidationProblemWithAKeyForEachOfManyElements_ConvertsEveryKeyInOrderWithinTheTimeout()
    {
        var errors = new Dictionary<string, string[]>(ManyKeys, StringComparer.Ordinal);
        for (var index = 0; index < ManyKeys; index++)
        {
            errors[$"Lines[{index}].Description"] = ["Enter a description."];
        }

        var problem = ValidationProblem(errors);

        ProblemDetailsCustomizer.Customize(Context(problem));

        Assert.Equal(Enumerable.Range(0, ManyKeys).Select(index => $"lines[{index}].description"), problem.Errors.Keys);
        Assert.All(problem.Errors.Values, messages => Assert.Equal(["Enter a description."], messages));
    }

    private static HttpValidationProblemDetails ValidationProblem(Dictionary<string, string[]> errors) =>
        new(errors) { Status = StatusCodes.Status400BadRequest };

    private static ProblemDetailsContext Context(ProblemDetails problem) =>
        new() { HttpContext = new DefaultHttpContext(), ProblemDetails = problem };
}
