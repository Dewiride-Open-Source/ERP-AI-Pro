using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.Errors;

public sealed class ProblemTypesTests
{
    [Fact]
    public void ToTypeUri_Code_IsTheCodeUnderTheProblemPrefix() =>
        Assert.Equal("/problems/invoice.already-issued", ProblemTypes.ToTypeUri("invoice.already-issued"));

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, ProblemTypes.RequestInvalid)]
    [InlineData(StatusCodes.Status404NotFound, ProblemTypes.ResourceNotFound)]
    [InlineData(StatusCodes.Status499ClientClosedRequest, ProblemTypes.RequestCancelled)]
    [InlineData(StatusCodes.Status500InternalServerError, ProblemTypes.ServerError)]
    [InlineData(StatusCodes.Status503ServiceUnavailable, ProblemTypes.ServerError)]
    public void DefaultCode_Status_IsTheCodeOfThatStatus(int status, string expected) =>
        Assert.Equal(expected, ProblemTypes.DefaultCode(status));

    [Theory]
    [InlineData(ProblemTypes.RequestInvalid)]
    [InlineData(ProblemTypes.RequestCancelled)]
    [InlineData(ProblemTypes.ResourceNotFound)]
    [InlineData(ProblemTypes.ServerError)]
    public void Codes_AreLowerCaseDottedSegments(string code) =>
        Assert.Matches("^[a-z]+(-[a-z]+)*(\\.[a-z]+(-[a-z]+)*)+$", code);
}
