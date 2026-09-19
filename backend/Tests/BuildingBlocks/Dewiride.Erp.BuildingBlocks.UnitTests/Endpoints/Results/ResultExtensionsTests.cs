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
