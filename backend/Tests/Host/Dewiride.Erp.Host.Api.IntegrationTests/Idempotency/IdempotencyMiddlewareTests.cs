using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Idempotency.Http;
using Microsoft.AspNetCore.Mvc;
using static Dewiride.Erp.Host.Api.IntegrationTests.Idempotency.IdempotentEndpointsFixture;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Idempotency;

public sealed class IdempotencyMiddlewareTests(IdempotentEndpointsFixture fixture) : IClassFixture<IdempotentEndpointsFixture>
{
    [Fact]
    public async Task Post_RepeatedWithTheSameKeyAndBody_ReplaysTheFirstResponseWithoutActingTwice()
    {
        var key = Guid.CreateVersion7();

        var first = await PostAsync(OrdersPath, key, new OrderRequest("pen"));
        var second = await PostAsync(OrdersPath, key, new OrderRequest("pen"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.False(first.Headers.Contains(IdempotencyKeyHeader.ReplayedName));
        Assert.Equal("true", Assert.Single(second.Headers.GetValues(IdempotencyKeyHeader.ReplayedName)));
        Assert.Equal(await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(first.Content.Headers.ContentType?.ToString(), second.Content.Headers.ContentType?.ToString());
        Assert.NotNull(second.Headers.Location);
        Assert.Equal(first.Headers.Location, second.Headers.Location);
        var replayed = await second.Content.ReadFromJsonAsync<OrderResponse>(TestContext.Current.CancellationToken);
        Assert.Equal("pen", replayed!.Item);
        Assert.EndsWith($"/{replayed.Number}", second.Headers.Location.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_SameKeyWithADifferentBody_Answers422KeyReused()
    {
        var key = Guid.CreateVersion7();
        await PostAsync(OrdersPath, key, new OrderRequest("pen"));

        var response = await PostAsync(OrdersPath, key, new OrderRequest("pencil"));

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, IdempotencyProblems.KeyReused);
    }

    [Fact]
    public async Task Post_SameKeyOnADifferentPath_Answers422KeyReused()
    {
        var key = Guid.CreateVersion7();
        await PostAsync(OrdersPath, key, new OrderRequest("pen"));

        var response = await PostAsync(FailingPath, key, new OrderRequest("pen"));

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, IdempotencyProblems.KeyReused);
    }

    [Fact]
    public async Task Post_WithoutAKey_Answers400KeyMissing()
    {
        var response = await fixture.Factory.CreateClient().PostAsJsonAsync(OrdersPath, new OrderRequest("pen"), TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, IdempotencyProblems.KeyMissing);
    }

    [Theory]
    [InlineData("not-a-uuid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("\"8e03978e-40d5-43e8-bc93-6894a57f9324")]
    public async Task Post_WithAMalformedKey_Answers400KeyInvalid(string header)
    {
        var response = await SendAsync(OrdersPath, header, new OrderRequest("pen"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, IdempotencyProblems.KeyInvalid);
    }

    [Fact]
    public async Task Post_QuotedStructuredFieldKey_IsAccepted()
    {
        var response = await SendAsync(OrdersPath, $"\"{Guid.CreateVersion7()}\"", new OrderRequest("pen"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Post_WhileTheFirstRequestIsStillRunning_Answers409InProgress()
    {
        var key = Guid.CreateVersion7();
        var first = PostAsync(SlowPath, key, new OrderRequest("pen"));
        await fixture.Entered.WaitAsync(TestContext.Current.CancellationToken);

        var second = await PostAsync(SlowPath, key, new OrderRequest("pen"));
        fixture.Gate.Release();
        var firstResponse = await first;

        await AssertProblemAsync(second, HttpStatusCode.Conflict, IdempotencyProblems.InProgress);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
    }

    [Theory]
    [InlineData("explode")]
    [InlineData("unavailable")]
    public async Task Post_FirstAttemptFailsWithAServerError_LetsTheClientRetryWithTheSameKey(string item)
    {
        var key = Guid.CreateVersion7();

        var first = await PostAsync(FailingPath, key, new OrderRequest(item));
        var second = await PostAsync(FailingPath, key, new OrderRequest(item));

        Assert.True((int)first.StatusCode >= 500);
        Assert.True((int)second.StatusCode >= 500);
        Assert.False(second.Headers.Contains(IdempotencyKeyHeader.ReplayedName));
    }

    [Fact]
    public async Task Post_ResponseLargerThanTheStoredLimit_AnswersTheCallerButCannotReplay()
    {
        var key = Guid.CreateVersion7();

        var first = await PostAsync(LargePath, key, new OrderRequest("big"));
        var second = await PostAsync(LargePath, key, new OrderRequest("big"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.True((await first.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).Length > MaxStoredResponseBytes);
        await AssertProblemAsync(second, HttpStatusCode.Conflict, IdempotencyProblems.ReplayUnavailable);
    }

    [Fact]
    public async Task Post_ReplayOfAStoredProblem_ReportsTheCorrelationIdOfTheReplay()
    {
        var key = Guid.CreateVersion7().ToString("D");

        using var first = await SendAsync(RejectedPath, key, new OrderRequest("pen"), "order-first");
        using var second = await SendAsync(RejectedPath, key, new OrderRequest("pen"), "order-replay");

        await AssertProblemAsync(first, HttpStatusCode.Conflict, "order.duplicate");
        await AssertProblemAsync(second, HttpStatusCode.Conflict, "order.duplicate");
        Assert.Equal("true", Assert.Single(second.Headers.GetValues(IdempotencyKeyHeader.ReplayedName)));
        Assert.Equal("order-first", await TraceIdAsync(first));
        Assert.Equal("order-replay", Assert.Single(second.Headers.GetValues(CorrelationId.HeaderName)));
        Assert.Equal("order-replay", await TraceIdAsync(second));
    }

    [Fact]
    public async Task Get_WithoutTheMetadata_IgnoresTheHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/platform/features");
        request.Headers.TryAddWithoutValidation(IdempotencyKeyHeader.Name, "not-a-uuid");

        var response = await fixture.Factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<HttpResponseMessage> PostAsync(string path, Guid key, OrderRequest body) => SendAsync(path, key.ToString("D"), body);

    private async Task<HttpResponseMessage> SendAsync(string path, string header, OrderRequest body, string? correlationId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation(IdempotencyKeyHeader.Name, header);
        if (correlationId is not null)
        {
            request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, correlationId);
        }

        return await fixture.Factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<string?> TraceIdAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("traceId").GetString();
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal(code, ((JsonElement)problem.Extensions["code"]!).GetString());
        Assert.Equal((int)status, problem.Status);
    }
}
