using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.Host.Api.IntegrationTests.Authentication;
using Dewiride.Erp.Host.Api.IntegrationTests.BearerTokens;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication.BearerTokens;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Permissions;

public sealed class BearerTokenPermissionTests(BearerTokenPermissionTests.Fixture fixture) : IClassFixture<BearerTokenPermissionTests.Fixture>
{
    [Fact]
    public async Task Get_PersonTokenOfAnEmployee_ReachesARouteRequiringAPermissionEveryEmployeeHolds()
    {
        var person = await PermissionRoutes.AdmittedPersonAsync(fixture.Factory, AppRoles.User);
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, PermissionRoutes.BearerFilesPath, PermissionRoutes.PersonToken(person));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Get_PersonTokenOfAPersonWithoutAnAppRole_AnswersForbiddenWithAChallengeNamingNoError()
    {
        var person = await PermissionRoutes.AdmittedPersonAsync(fixture.Factory);
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, PermissionRoutes.BearerFilesPath, PermissionRoutes.PersonToken(person));

        await AssertForbiddenAsync(response, BearerTokenEvents.NoTokenChallenge);
    }

    [Fact]
    public async Task Get_PersonTokenOfAPersonWithoutAnAppRole_RecordsAMissingPermissionOfThePersonAndTheirClientApplication()
    {
        var person = await PermissionRoutes.AdmittedPersonAsync(fixture.Factory);
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, PermissionRoutes.BearerFilesPath, PermissionRoutes.PersonToken(person));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var entry = Assert.Single(await SecurityEventRecords.OfAsync(fixture.Factory.Services, response));
        Assert.Equal(
            (SecurityEventKind.BearerTokenRefused, BearerTokenEvents.MissingPermissionRefusal, person.ObjectId, TestApplications.NativeClient.ClientId),
            (entry.Kind, entry.Detail, entry.ActorObjectId, entry.ClientApplicationId));
    }

    [Fact]
    public async Task Get_ApplicationTokenWithTheRoutesRole_AnswersForbiddenAndRecordsAMissingPermission()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, PermissionRoutes.BearerFilesPath, BearerTokenRoutes.ApplicationToken(BearerTokenRoutes.IntegrationRole, AppRoles.User));

        await AssertForbiddenAsync(response, BearerTokenEvents.NoTokenChallenge);
        var entry = Assert.Single(await SecurityEventRecords.OfAsync(fixture.Factory.Services, response));
        Assert.Equal(
            (SecurityEventKind.BearerTokenRefused, BearerTokenEvents.MissingPermissionRefusal, TestApplications.Integration.ObjectId, TestApplications.Integration.ClientId),
            (entry.Kind, entry.Detail, entry.ActorObjectId, entry.ClientApplicationId));
    }

    [Fact]
    public async Task Get_ApplicationTokenWithoutTheRoutesRole_AnswersAnInsufficientScope()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, PermissionRoutes.BearerFilesPath, BearerTokenRoutes.ApplicationToken("Erp.Test.Other"));

        await AssertForbiddenAsync(response, BearerTokenEvents.InsufficientScopeChallenge);
        var entry = Assert.Single(await SecurityEventRecords.OfAsync(fixture.Factory.Services, response));
        Assert.Equal(BearerTokenEvents.InsufficientScopeRefusal, entry.Detail);
    }

    private static async Task AssertForbiddenAsync(HttpResponseMessage response, string challenge)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal([challenge], response.Headers.WwwAuthenticate.Select(value => value.ToString()));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(ProblemTypes.RequestForbidden, body.RootElement.GetProperty("code").GetString());
        Assert.Equal(AuthenticationProblems.ForbiddenTitle, body.RootElement.GetProperty("title").GetString());
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = PermissionRoutes.Factory();

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
