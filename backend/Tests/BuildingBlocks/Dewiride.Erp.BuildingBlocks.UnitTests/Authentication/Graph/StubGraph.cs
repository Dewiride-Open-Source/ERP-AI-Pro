using Dewiride.Erp.BuildingBlocks.Authentication.Graph;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.Graph;

internal sealed class StubGraph(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
{
    public List<GraphRequest> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(new GraphRequest(
            request.RequestUri!,
            request.Headers.TryGetValues(MicrosoftGraph.ConsistencyLevelHeader, out var levels) ? string.Join(',', levels) : null,
            request.Headers.Authorization?.ToString()));

        return Task.FromResult(answer(request));
    }
}
