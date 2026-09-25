using System.Net;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Testing;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Pipeline;

public sealed class ProblemDetailsTests : IClassFixture<ProblemDetailsTests.Fixture>
{
    private const string ProblemEndpoint = "/__test/conflict";

    private readonly HttpClient _client;

    public ProblemDetailsTests(Fixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task Get_UnknownRoute_AnswersTheNotFoundProblemType()
    {
        using var response = await _client.GetAsync(new Uri("/api/platform/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadAsync(response);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Equal("/problems/resource.not-found", problem.Type);
        Assert.Equal(ProblemTypes.ResourceNotFound, problem.Extensions[ResultExtensions.CodeExtension]?.ToString());
        Assert.Equal("/api/platform/does-not-exist", problem.Instance);
        Assert.Equal(Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)), problem.Extensions["traceId"]?.ToString());
    }

    [Fact]
    public async Task Post_RouteThatOnlyAllowsGet_AnswersAClientErrorProblemType()
    {
        using var response = await _client.PostAsync(new Uri("/api/platform/system-info", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadAsync(response);
        Assert.Equal("/problems/request.method-not-allowed", problem.Type);
        Assert.Equal(ProblemTypes.RequestMethodNotAllowed, problem.Extensions[ResultExtensions.CodeExtension]?.ToString());
        Assert.Equal(Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)), problem.Extensions["traceId"]?.ToString());
    }

    [Fact]
    public async Task Get_ThrowingRoute_AnswersTheServerErrorProblemType()
    {
        using var response = await _client.GetAsync(new Uri(ErpApiFactory.ThrowingPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = await ReadAsync(response);
        Assert.Equal("/problems/server.error", problem.Type);
        Assert.Equal(ProblemTypes.ServerError, problem.Extensions[ResultExtensions.CodeExtension]?.ToString());
        Assert.Equal(ErpApiFactory.ThrowingPath, problem.Instance);
        Assert.Equal(Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)), problem.Extensions["traceId"]?.ToString());
    }

    [Fact]
    public async Task Get_EndpointReturningAnError_AnswersTheProblemTypeOfThatErrorCode()
    {
        using var response = await _client.GetAsync(new Uri(ProblemEndpoint, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadAsync(response);
        Assert.Equal("/problems/invoice.already-issued", problem.Type);
        Assert.Equal("invoice.already-issued", problem.Extensions[ResultExtensions.CodeExtension]?.ToString());
        Assert.Equal("The invoice was already issued.", problem.Detail);
        Assert.Equal(ProblemEndpoint, problem.Instance);
        Assert.Equal(Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)), problem.Extensions["traceId"]?.ToString());
    }

    private static async Task<ProblemDetails> ReadAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);

        return problem;
    }

    public sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Factory = new ErpApiFactory().WithTestEndpoints(routes =>
                routes.MapGet(ProblemEndpoint, () => Error.Conflict("invoice.already-issued", "The invoice was already issued.").ToProblem()));
        }

        public ErpApiFactory Factory { get; }

        public void Dispose() => Factory.Dispose();
    }
}
