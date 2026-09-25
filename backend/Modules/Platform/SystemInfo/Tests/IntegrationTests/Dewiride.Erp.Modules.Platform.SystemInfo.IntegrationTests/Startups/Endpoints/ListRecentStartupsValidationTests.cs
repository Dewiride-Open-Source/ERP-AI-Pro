using System.Net;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.IntegrationTests.Startups.Endpoints;

public sealed class ListRecentStartupsValidationTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private const string Route = "/api/platform/system-info/startups";

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Get_TakeOutsideTheAllowedRange_AnswersValidationProblemDetails(int take)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri($"{Route}?take={take}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("/problems/request.invalid", problem.Type);
        Assert.Equal("request.invalid", problem.Extensions["code"]?.ToString());
        Assert.Equal(Route, problem.Instance);
        Assert.Equal(Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)), problem.Extensions["traceId"]?.ToString());
        var field = Assert.Single(problem.Errors);
        Assert.Equal("take", field.Key);
        Assert.Contains("between 1 and 100", Assert.Single(field.Value), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    public async Task Get_TakeThatIsNotAnInteger_AnswersMalformedRequestProblem(string take)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri($"{Route}?take={take}", UriKind.Relative), TestContext.Current.CancellationToken);

        await AssertMalformedAsync(response);
    }

    [Fact]
    public async Task Get_TakeThatIsNotAnIntegerInProduction_AnswersTheSameMalformedRequestProblem()
    {
        using var production = ErpApiFactory.ForEnvironment(Environments.Production);
        using var client = production.CreateClient();

        using var response = await client.GetAsync(new Uri($"{Route}?take=many", UriKind.Relative), TestContext.Current.CancellationToken);

        await AssertMalformedAsync(response);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?take=1")]
    [InlineData("?take=100")]
    public async Task Get_TakeInsideTheAllowedRange_AnswersTheStartups(string query)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(Route + query, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task AssertMalformedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal("/problems/request.malformed", problem.Type);
        Assert.Equal("request.malformed", problem.Extensions["code"]?.ToString());
        Assert.Equal(Route, problem.Instance);
    }
}
