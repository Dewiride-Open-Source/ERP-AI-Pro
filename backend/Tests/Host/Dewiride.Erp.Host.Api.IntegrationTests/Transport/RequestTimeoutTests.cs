using System.Net;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Sql;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Transport;

public sealed class RequestTimeoutTests : IClassFixture<RequestTimeoutTests.Fixture>
{
    private const string SlowPath = "/__test/slow";

    private const string SlowQueryPath = "/__test/slow-query";

    private const string TimedPath = "/__test/timed";

    private const string ExemptPath = "/__test/exempt";

    private readonly HttpClient _client;

    public RequestTimeoutTests(Fixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Theory]
    [InlineData(SlowPath)]
    [InlineData(SlowQueryPath)]
    public async Task Get_RequestRunningPastTheTimeout_AnswersARequestTimeoutProblemFromTheOuterHandlers(string path)
    {
        using var response = await _client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("/problems/request.timeout", problem!.Type);
        Assert.Equal(ProblemTypes.RequestTimeout, problem.Extensions["code"]?.ToString());
        Assert.Equal(Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)), problem.Extensions[ProblemTypes.TraceIdExtension]?.ToString());
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Theory]
    [InlineData(TimedPath, true)]
    [InlineData(ExemptPath, false)]
    public async Task Get_Endpoint_RunsUnderTheDefaultTimeoutUnlessItsMetadataDisablesIt(string path, bool timed)
    {
        using var response = await _client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(timed, await response.Content.ReadFromJsonAsync<bool>(TestContext.Current.CancellationToken));
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Factory = new ErpApiFactory()
                .WithConfiguration($"{ErpHostOptions.SectionName}:RequestTimeout", "00:00:01")
                .WithTestEndpoints(routes =>
                {
                    routes.MapGet(SlowPath, async (CancellationToken cancellationToken) =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                        return Results.Ok();
                    });
                    routes.MapGet(SlowQueryPath, async (CancellationToken cancellationToken) =>
                    {
                        await using var connection = new SqlConnection(SqlTestDatabase.Current.ConnectionString);
                        await connection.OpenAsync(cancellationToken);
                        await using var command = new SqlCommand("WAITFOR DELAY '00:00:30'", connection) { CommandTimeout = 60 };
                        await command.ExecuteNonQueryAsync(cancellationToken);
                        return Results.Ok();
                    });
                    routes.MapGet(TimedPath, (HttpContext context) => context.Features.Get<IHttpRequestTimeoutFeature>() is not null);
                    routes.MapGet(ExemptPath, (HttpContext context) => context.Features.Get<IHttpRequestTimeoutFeature>() is not null).DisableRequestTimeout();
                });
        }

        public ErpApiFactory Factory { get; }

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
