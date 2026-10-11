using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.Host.Api.IntegrationTests.Admission;
using Dewiride.Erp.Host.Api.IntegrationTests.BearerTokens;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Permissions;

public sealed class AssignedRoleTests(AssignedRoleTests.Fixture fixture) : IClassFixture<AssignedRoleTests.Fixture>
{
    private const string FilesPolicyPath = "/api/platform/attachments/policy";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    [Fact]
    public async Task Get_RoleAssignedToTheRecordOfASignedInPerson_CountsFromTheirNextRequest()
    {
        var person = PersonRecords.NewPerson(fixture.Factory);
        using var client = await SignedInClientAsync(person);
        Assert.Equal([ErpRole.Employee], await RolesAsync(client));

        await PersonRecords.AssignRolesAsync(fixture.Factory, person, "manager", "finance");

        Assert.Equal([ErpRole.Employee, ErpRole.Finance, ErpRole.Manager], await RolesAsync(client));
    }

    [Fact]
    public async Task Get_RoleTakenFromTheRecordOfASignedInPerson_StopsCountingFromTheirNextRequest()
    {
        var person = PersonRecords.NewPerson(fixture.Factory);
        using var client = await SignedInClientAsync(person);
        await PersonRecords.AssignRolesAsync(fixture.Factory, person, "hr");
        Assert.Equal([ErpRole.Employee, ErpRole.Hr], await RolesAsync(client));

        await PersonRecords.AssignRolesAsync(fixture.Factory, person);

        Assert.Equal([ErpRole.Employee], await RolesAsync(client));
    }

    [Fact]
    public async Task Get_PermissionRouteWithASessionWhoseAccountTheTokenCacheNoLongerHolds_AnswersUnauthenticated()
    {
        var person = PersonRecords.NewPerson(fixture.Factory);
        using var client = await SignedInClientAsync(person);
        using (var served = await client.GetAsync(new Uri(FilesPolicyPath, UriKind.Relative), TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        }

        await TestSignIn.ForgetAccountAsync(fixture.Factory.Services, person);

        using var response = await client.GetAsync(new Uri(FilesPolicyPath, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_PersonTokenOfAPersonWhoseRecordHoldsARole_CarriesTheRoleIntoItsRequest()
    {
        var person = await PermissionRoutes.AdmittedPersonAsync(fixture.Factory, AppRoles.User);
        await PersonRecords.AssignRolesAsync(fixture.Factory, person, "manager");
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, PermissionRoutes.BearerRolesPath, PermissionRoutes.PersonToken(person));

        Assert.Equal([ErpRole.Employee, ErpRole.Manager], await ReadRolesAsync(response));
    }

    [Fact]
    public async Task Get_ApplicationTokenNamingThePersonAppRoles_HoldsNoRole()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await BearerTokenRoutes.SendAsync(
            client,
            HttpMethod.Get,
            PermissionRoutes.BearerRolesPath,
            BearerTokenRoutes.ApplicationToken(BearerTokenRoutes.IntegrationRole, AppRoles.User, AppRoles.Administrator));

        Assert.Empty(await ReadRolesAsync(response));
    }

    private async Task<HttpClient> SignedInClientAsync(TestUser person)
    {
        var client = TestSignIn.CreateClient(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, person);

        return client;
    }

    private static async Task<ErpRole[]> RolesAsync(HttpClient client)
    {
        using var response = await client.GetAsync(new Uri(PermissionRoutes.RolesPath, UriKind.Relative), TestContext.Current.CancellationToken);

        return await ReadRolesAsync(response);
    }

    private static async Task<ErpRole[]> ReadRolesAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var roles = await response.Content.ReadFromJsonAsync<ErpRole[]>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(roles);

        return roles;
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = PermissionRoutes.Factory();

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
