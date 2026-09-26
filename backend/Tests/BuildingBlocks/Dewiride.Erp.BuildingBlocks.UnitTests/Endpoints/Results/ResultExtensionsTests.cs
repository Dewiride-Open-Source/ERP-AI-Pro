using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.Results;

public sealed class ResultExtensionsTests
{
    [Theory]
    [InlineData(ErrorKind.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorKind.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorKind.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorKind.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorKind.TooLarge, StatusCodes.Status413PayloadTooLarge)]
    [InlineData(ErrorKind.UnsupportedType, StatusCodes.Status415UnsupportedMediaType)]
    [InlineData(ErrorKind.Failure, StatusCodes.Status422UnprocessableEntity)]
    public void ToProblem_MapsEveryKindToAStatusAndCarriesTheCode(ErrorKind kind, int expectedStatus)
    {
        var error = new Error("sample.code", "Something specific happened.", kind);

        var problem = error.ToProblem();

        Assert.Equal(expectedStatus, problem.StatusCode);
        Assert.Equal(error.Message, problem.ProblemDetails.Detail);
        Assert.Equal(error.Code, problem.ProblemDetails.Extensions["code"]);
    }

    [Fact]
    public void ToProblem_ValidationErrorWithFields_IsAValidationProblemWithErrorsPerField()
    {
        var error = Error.Validation("request.invalid", "The request is invalid.", new Dictionary<string, string[]>(StringComparer.Ordinal) { ["Name"] = ["required"], ["Age"] = ["too low", "not even"] });

        var problem = error.ToProblem();

        var validation = Assert.IsType<HttpValidationProblemDetails>(problem.ProblemDetails);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("The request is invalid.", validation.Detail);
        Assert.Equal("request.invalid", validation.Extensions["code"]);
        Assert.Equal(["required"], validation.Errors["Name"]);
        Assert.Equal(["too low", "not even"], validation.Errors["Age"]);
    }

    [Fact]
    public void ToProblem_ValidationErrorWithoutFields_IsAPlainProblem()
    {
        var problem = Error.Validation("query.invalid-sort", "bad sort").ToProblem();

        Assert.IsNotType<HttpValidationProblemDetails>(problem.ProblemDetails);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public void ToCreatedResult_Success_IsCreatedAtTheLocation()
    {
        var result = Result.Success(42).ToCreatedResult(id => $"/api/things/{id}");

        var created = Assert.IsType<Created<int>>(result.Result);
        Assert.Equal("/api/things/42", created.Location);
        Assert.Equal(42, created.Value);
    }

    [Fact]
    public void ToCreatedResult_Failure_IsProblem()
    {
        var result = Result.Fail<int>(Error.Conflict("x", "taken")).ToCreatedResult(id => $"/api/things/{id}");

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }

    [Fact]
    public void ToHttpResult_SuccessWithValue_IsOk()
    {
        var result = Result.Success("payload").ToHttpResult();

        var ok = Assert.IsType<Ok<string>>(result.Result);
        Assert.Equal("payload", ok.Value);
    }

    [Fact]
    public void ToHttpResult_FailureWithValue_IsProblem()
    {
        var result = Result.Fail<string>(Error.NotFound("x", "missing")).ToHttpResult();

        var problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public void ToHttpResult_NonGeneric_MapsSuccessToNoContentAndFailureToProblem()
    {
        Assert.IsType<NoContent>(Result.Success().ToHttpResult().Result);

        var problem = Assert.IsType<ProblemHttpResult>(Result.Fail(Error.Conflict("x", "taken")).ToHttpResult().Result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }
}
