using System.Net;
using Dewiride.Erp.Testing;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Pipeline;

public sealed class ProblemDetailsTests : IClassFixture<ErpApiFactory>
{
    private readonly HttpClient _client;

    public ProblemDetailsTests(ErpApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_UnknownRoute_ReturnsProblemDetails()
    {
        using var response = await _client.GetAsync(new Uri("/api/platform/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }
}
