using System.Text;
using Dewiride.Erp.BuildingBlocks.Idempotency.Http;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Idempotency.Http;

public sealed class RequestFingerprintTests
{
    private static readonly Guid Actor = new("0199a1b2-0000-7000-8000-0000000000aa");

    [Fact]
    public async Task ComputeAsync_SameRequestTwice_IsStableAndLeavesTheBodyReadable()
    {
        var request = Request("POST", "/api/orders", "?x=1", "{\"item\":\"pen\"}");

        var first = await RequestFingerprint.ComputeAsync(request, Actor, TestContext.Current.CancellationToken);
        var second = await RequestFingerprint.ComputeAsync(request, Actor, TestContext.Current.CancellationToken);

        Assert.Equal(32, first.Length);
        Assert.Equal(first, second);
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        Assert.Equal("{\"item\":\"pen\"}", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("PUT", "/api/orders", "?x=1", "{\"item\":\"pen\"}")]
    [InlineData("POST", "/api/orders/", "?x=1", "{\"item\":\"pen\"}")]
    [InlineData("POST", "/api/orders", "?x=2", "{\"item\":\"pen\"}")]
    [InlineData("POST", "/api/orders", "?x=1", "{\"item\":\"pencil\"}")]
    public async Task ComputeAsync_DifferentMethodPathQueryOrBody_Differs(string method, string path, string query, string body)
    {
        var baseline = await RequestFingerprint.ComputeAsync(Request("POST", "/api/orders", "?x=1", "{\"item\":\"pen\"}"), Actor, TestContext.Current.CancellationToken);

        var other = await RequestFingerprint.ComputeAsync(Request(method, path, query, body), Actor, TestContext.Current.CancellationToken);

        Assert.NotEqual(baseline, other);
    }

    [Fact]
    public async Task ComputeAsync_DifferentActor_Differs()
    {
        var mine = await RequestFingerprint.ComputeAsync(Request("POST", "/api/orders", "", "{}"), Actor, TestContext.Current.CancellationToken);

        var theirs = await RequestFingerprint.ComputeAsync(Request("POST", "/api/orders", "", "{}"), Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        Assert.NotEqual(mine, theirs);
    }

    [Fact]
    public async Task ComputeAsync_HeadersOtherThanTheBody_DoNotInfluenceTheFingerprint()
    {
        var plain = Request("POST", "/api/orders", "", "{}");
        var withHeaders = Request("POST", "/api/orders", "", "{}");
        withHeaders.Headers.UserAgent = "curl";
        withHeaders.Headers["X-Custom"] = "value";

        Assert.Equal(
            await RequestFingerprint.ComputeAsync(plain, Actor, TestContext.Current.CancellationToken),
            await RequestFingerprint.ComputeAsync(withHeaders, Actor, TestContext.Current.CancellationToken));
    }

    private static HttpRequest Request(string method, string path, string query, string body)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        return context.Request;
    }
}
