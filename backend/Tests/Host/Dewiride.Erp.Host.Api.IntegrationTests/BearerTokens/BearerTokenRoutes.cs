using System.Net.Http.Headers;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authentication.BearerTokens;

namespace Dewiride.Erp.Host.Api.IntegrationTests.BearerTokens;

internal static class BearerTokenRoutes
{
    public const string ReadScope = "Erp.Test.Read";

    public const string IntegrationRole = "Erp.Test.Integration";

    public const string Path = "/__test/bearer";

    public const string SessionPath = "/__test/session";

    public static BearerTokenAccess Access { get; } = new([ReadScope], [IntegrationRole]);

    public static void Map(IEndpointRouteBuilder routes)
    {
        TestSignIn.Map(routes);
        routes.MapMethods(Path, ["GET", "POST"], (IActorContext actor) => Results.Ok(new ObservedActor(actor.ActorId, actor.IsAuthenticated)))
            .RequireBearerToken(Access);
        routes.MapMethods(SessionPath, ["GET", "POST"], (IActorContext actor) => Results.Ok(new ObservedActor(actor.ActorId, actor.IsAuthenticated)));
    }

    public static ErpApiFactory Factory(bool bearerTokensEnabled = true) =>
        new ErpApiFactory().WithFeature(BearerTokenFeature.Name, bearerTokensEnabled).WithTestEndpoints(Map);

    public static string PersonToken(params string[] scopes) => TestTokenIssuer.ForPerson(TestUsers.Accountant, TestApplications.NativeClient, scopes);

    public static string ApplicationToken(params string[] roles) => TestTokenIssuer.ForApplication(TestApplications.Integration, roles);

    public static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? token)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public sealed record ObservedActor(Guid ActorId, bool IsAuthenticated);
}
