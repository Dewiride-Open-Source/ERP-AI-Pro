using System.Globalization;
using System.Net;
using System.Text;
using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.Testing;
using Microsoft.AspNetCore.Mvc;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Transport;

public sealed class RequestLimitsTests : IClassFixture<RequestLimitsTests.Fixture>
{
    private const int Limit = 1024;

    private const string BoundPath = "/__test/bound-body";

    private const string ReadPath = "/__test/read-body";

    private readonly HttpClient _client;

    public RequestLimitsTests(Fixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Theory]
    [InlineData(BoundPath)]
    [InlineData(ReadPath)]
    public async Task Post_BodyOverTheLimit_AnswersRequestTooLarge(string path)
    {
        using var response = await PostAsync(path, Limit * 4);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("/problems/request.too-large", problem!.Type);
        Assert.Equal(ProblemTypes.RequestTooLarge, problem.Extensions["code"]?.ToString());
        Assert.Equal("Content Too Large", problem.Title);
        Assert.Equal(Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)), problem.Extensions[ProblemTypes.TraceIdExtension]?.ToString());
    }

    [Theory]
    [InlineData(BoundPath)]
    [InlineData(ReadPath)]
    public async Task Post_BodyUnderTheLimit_IsRead(string path)
    {
        using var response = await PostAsync(path, Limit / 2);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpResponseMessage> PostAsync(string path, int textLength)
    {
        var body = $$"""{"text":"{{new string('x', textLength)}}"}""";
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        return await _client.PostAsync(new Uri(path, UriKind.Relative), content, TestContext.Current.CancellationToken);
    }

    public sealed record Payload(string Text);

    public sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Factory = new ErpApiFactory()
                .WithConfiguration($"{ErpHostOptions.SectionName}:MaxRequestBodyBytes", Limit.ToString(CultureInfo.InvariantCulture))
                .WithTestEndpoints(routes =>
                {
                    routes.MapPost(BoundPath, (Payload payload) => Results.Ok(payload.Text.Length));
                    routes.MapPost(ReadPath, async (HttpRequest request) =>
                    {
                        using var reader = new StreamReader(request.Body);
                        return Results.Ok((await reader.ReadToEndAsync(request.HttpContext.RequestAborted)).Length);
                    });
                })
                .WithKestrel();
        }

        public ErpApiFactory Factory { get; }

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
