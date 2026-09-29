using System.Net;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Actors;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Actors;

public sealed class ActorContextTests(ActorContextTests.Fixture fixture) : IClassFixture<ActorContextTests.Fixture>
{
    private const string ActorPath = "/__test/actor";

    private const string AnonymousActorPath = "/__test/anonymous-actor";

    [Fact]
    public void Services_ActorContext_IsTheHttpActorAndReportsTheSystemOutsideARequest()
    {
        using var scope = fixture.Factory.Services.CreateScope();

        var actor = scope.ServiceProvider.GetRequiredService<IActorContext>();

        Assert.IsType<HttpActorContext>(actor);
        Assert.Equal(ActorIds.System, actor.ActorId);
        Assert.False(actor.IsAuthenticated);
        Assert.Single(scope.ServiceProvider.GetServices<IActorContext>());
    }

    [Fact]
    public async Task Get_SignedInRequest_ReportsTheObjectIdOfThePerson()
    {
        using var client = fixture.Factory.CreateClient().AsUser(TestUsers.Accountant);

        var actor = await ReadActorAsync(client, ActorPath);

        Assert.Equal(new ObservedActor(TestUsers.Accountant.ObjectId, true), actor);
    }

    [Fact]
    public async Task Get_AnonymousRequestToAnAnonymousRoute_ReportsTheAnonymousActor()
    {
        using var client = fixture.Factory.CreateClient();

        var actor = await ReadActorAsync(client, AnonymousActorPath);

        Assert.Equal(new ObservedActor(ActorIds.Anonymous, false), actor);
    }

    private static async Task<ObservedActor> ReadActorAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actor = await response.Content.ReadFromJsonAsync<ObservedActor>(TestContext.Current.CancellationToken);
        Assert.NotNull(actor);

        return actor;
    }

    public sealed record ObservedActor(Guid ActorId, bool IsAuthenticated);

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithTestEndpoints(routes =>
        {
            routes.MapGet(ActorPath, (IActorContext actor) => new ObservedActor(actor.ActorId, actor.IsAuthenticated));
            routes.MapGet(AnonymousActorPath, (IActorContext actor) => new ObservedActor(actor.ActorId, actor.IsAuthenticated)).AllowAnonymous();
        });

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
