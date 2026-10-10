using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.Host.Api.IntegrationTests.Authentication;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authentication.BearerTokens;

namespace Dewiride.Erp.Host.Api.IntegrationTests.BearerTokens;

public sealed class BearerTokenAccessTests(BearerTokenAccessTests.Fixture fixture) : IClassFixture<BearerTokenAccessTests.Fixture>
{
    [Fact]
    public async Task Get_PersonTokenWithAnotherScope_AnswersForbiddenNamingAnInsufficientScope()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, BearerTokenRoutes.Path, BearerTokenRoutes.PersonToken("Erp.Test.Write"));

        await AssertForbiddenAsync(response);
    }

    [Fact]
    public async Task Get_PersonTokenWithAnotherScope_RecordsAnInsufficientScopeOfThePersonAndTheirClientApplication()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, BearerTokenRoutes.Path, BearerTokenRoutes.PersonToken("Erp.Test.Write"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var entry = Assert.Single(await SecurityEventRecords.OfAsync(fixture.Factory.Services, response));
        Assert.Equal(
            (SecurityEventKind.BearerTokenRefused, BearerTokenEvents.InsufficientScopeRefusal, TestUsers.Accountant.ObjectId, TestApplications.NativeClient.ClientId),
            (entry.Kind, entry.Detail, entry.ActorObjectId, entry.ClientApplicationId));
    }

    [Fact]
    public async Task Get_ApplicationTokenWithAnotherRole_RecordsAnInsufficientScopeOfTheApplication()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, BearerTokenRoutes.Path, BearerTokenRoutes.ApplicationToken("Erp.Test.Other"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var entry = Assert.Single(await SecurityEventRecords.OfAsync(fixture.Factory.Services, response));
        Assert.Equal(
            (SecurityEventKind.BearerTokenRefused, BearerTokenEvents.InsufficientScopeRefusal, TestApplications.Integration.ObjectId, TestApplications.Integration.ClientId),
            (entry.Kind, entry.Detail, entry.ActorObjectId, entry.ClientApplicationId));
    }

    [Fact]
    public async Task Get_PersonTokenHoldingTheApplicationRoleAsTheirOwnRole_AnswersForbidden()
    {
        var claims = TestTokenIssuer.PersonClaims(TestUsers.Accountant, TestApplications.NativeClient, ["Erp.Test.Write"]);
        claims["roles"] = new[] { BearerTokenRoutes.IntegrationRole };
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, BearerTokenRoutes.Path, TestTokenIssuer.Issue(claims));

        await AssertForbiddenAsync(response);
    }

    [Fact]
    public async Task Get_ApplicationTokenWithAnotherRole_AnswersForbidden()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, BearerTokenRoutes.Path, BearerTokenRoutes.ApplicationToken("Erp.Test.Other"));

        await AssertForbiddenAsync(response);
    }

    [Fact]
    public async Task Get_ApplicationTokenWithoutTheTokenTypeClaim_IsToldApartByItsSubjectAndReachesTheRoute()
    {
        var claims = TestTokenIssuer.ApplicationClaims(TestApplications.Integration, [BearerTokenRoutes.IntegrationRole]);
        claims.Remove("idtyp");
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, BearerTokenRoutes.Path, TestTokenIssuer.Issue(claims));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("GET")]
    public async Task Send_PersonTokenWithTheScopeAndNoAntiforgeryToken_ReachesTheRoute(string method)
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, new HttpMethod(method), BearerTokenRoutes.Path, BearerTokenRoutes.PersonToken("Erp.Test.Write", BearerTokenRoutes.ReadScope));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task AssertForbiddenAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal(["Bearer error=\"insufficient_scope\""], response.Headers.WwwAuthenticate.Select(value => value.ToString()));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(ProblemTypes.RequestForbidden, body.RootElement.GetProperty("code").GetString());
        Assert.Equal(AuthenticationProblems.ForbiddenTitle, body.RootElement.GetProperty("title").GetString());
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = BearerTokenRoutes.Factory();

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
