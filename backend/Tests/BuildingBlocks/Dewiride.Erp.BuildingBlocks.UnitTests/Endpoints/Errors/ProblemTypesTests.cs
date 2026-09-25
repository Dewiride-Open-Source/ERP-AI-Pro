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
    [InlineData(StatusCodes.Status401Unauthorized, ProblemTypes.RequestUnauthenticated)]
    [InlineData(StatusCodes.Status403Forbidden, ProblemTypes.RequestForbidden)]
    [InlineData(StatusCodes.Status404NotFound, ProblemTypes.ResourceNotFound)]
    [InlineData(StatusCodes.Status405MethodNotAllowed, ProblemTypes.RequestMethodNotAllowed)]
    [InlineData(StatusCodes.Status409Conflict, ProblemTypes.RequestRejected)]
    [InlineData(StatusCodes.Status413PayloadTooLarge, ProblemTypes.RequestTooLarge)]
    [InlineData(StatusCodes.Status415UnsupportedMediaType, ProblemTypes.RequestUnsupportedMediaType)]
    [InlineData(StatusCodes.Status422UnprocessableEntity, ProblemTypes.RequestRejected)]
    [InlineData(StatusCodes.Status429TooManyRequests, ProblemTypes.RateLimitExceeded)]
    [InlineData(StatusCodes.Status499ClientClosedRequest, ProblemTypes.RequestCancelled)]
    [InlineData(StatusCodes.Status500InternalServerError, ProblemTypes.ServerError)]
    [InlineData(StatusCodes.Status502BadGateway, ProblemTypes.ServerError)]
    [InlineData(StatusCodes.Status503ServiceUnavailable, ProblemTypes.ServiceUnavailable)]
    public void DefaultCode_Status_IsTheCodeOfThatStatus(int status, string expected) =>
        Assert.Equal(expected, ProblemTypes.DefaultCode(status));

    [Theory]
    [InlineData(ProblemTypes.RequestInvalid)]
    [InlineData(ProblemTypes.RequestMalformed)]
    [InlineData(ProblemTypes.RequestUnauthenticated)]
    [InlineData(ProblemTypes.RequestForbidden)]
    [InlineData(ProblemTypes.RequestMethodNotAllowed)]
    [InlineData(ProblemTypes.RequestTooLarge)]
    [InlineData(ProblemTypes.RequestUnsupportedMediaType)]
    [InlineData(ProblemTypes.RequestRejected)]
    [InlineData(ProblemTypes.RequestCancelled)]
    [InlineData(ProblemTypes.RateLimitExceeded)]
    [InlineData(ProblemTypes.ResourceNotFound)]
    [InlineData(ProblemTypes.ServiceUnavailable)]
    [InlineData(ProblemTypes.ServerError)]
    public void Codes_AreLowerCaseDottedSegments(string code) =>
        Assert.Matches("^[a-z]+(-[a-z]+)*(\\.[a-z]+(-[a-z]+)*)+$", code);
}
