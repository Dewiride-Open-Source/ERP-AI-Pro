using System.Net;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.Modules.Platform.Numbering.Contracts;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authorization;

namespace Dewiride.Erp.Modules.Platform.Numbering.IntegrationTests.Series.Endpoints;

public sealed class NumberingPermissionTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Theory]
    [InlineData("GET", "/api/platform/numbering/documents", NumberingPermissions.Read)]
    [InlineData("GET", "/api/platform/numbering/series/{id:guid}", NumberingPermissions.Read)]
    [InlineData("POST", "/api/platform/numbering/series", NumberingPermissions.Change)]
    [InlineData("PUT", "/api/platform/numbering/series/{id:guid}", NumberingPermissions.Change)]
    public void Route_OfEachUseCase_RequiresItsOwnPermission(string method, string route, string permission)
    {
        Assert.Equal(permission, RoutePermissions.Of(factory.Services, method, route));
    }

    [Theory]
    [InlineData(NumberingPermissions.Read)]
    [InlineData(NumberingPermissions.Change)]
    public void PermissionsOf_Roles_GiveTheNumberingToFinanceAndAdministratorsAlone(string permission)
    {
        var catalog = factory.Services.GetRequiredService<PermissionCatalog>();

        Assert.Contains(permission, catalog.PermissionsOf([ErpRole.Finance]));
        Assert.Contains(permission, catalog.PermissionsOf([ErpRole.Administrator]));
        Assert.DoesNotContain(permission, catalog.PermissionsOf([ErpRole.Employee, ErpRole.Hr, ErpRole.Manager]));
    }

    [Theory]
    [InlineData("GET", "/documents")]
    [InlineData("GET", "/series/{id}")]
    [InlineData("POST", "/series")]
    [InlineData("PUT", "/series/{id}")]
    public async Task Request_AsAnEmployeeWithoutThePermission_AnswersForbidden(string method, string route)
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);
        using var request = new HttpRequestMessage(new HttpMethod(method), NumberingApi.RouteFor(route));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await NumberingApi.AssertProblemAsync(response, HttpStatusCode.Forbidden, "request.forbidden");
    }

    [Theory]
    [InlineData("GET", "/documents")]
    [InlineData("GET", "/series/{id}")]
    [InlineData("POST", "/series")]
    [InlineData("PUT", "/series/{id}")]
    public async Task Request_Anonymous_AnswersUnauthenticated(string method, string route)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), NumberingApi.RouteFor(route));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
