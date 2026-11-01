using System.Net;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.Modules.Platform.Jobs.Contracts;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authorization;

namespace Dewiride.Erp.Modules.Platform.Jobs.IntegrationTests.Runs.Endpoints;

public sealed class JobsPermissionTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Theory]
    [InlineData("GET", "/api/platform/jobs", JobsPermissions.Read)]
    [InlineData("GET", "/api/platform/jobs/runs", JobsPermissions.Read)]
    [InlineData("GET", "/api/platform/jobs/runs/{id:guid}", JobsPermissions.Read)]
    [InlineData("POST", "/api/platform/jobs/runs/{id:guid}/rerun", JobsPermissions.Rerun)]
    [InlineData("POST", "/api/platform/jobs/runs/{id:guid}/cancel", JobsPermissions.Cancel)]
    public void Route_OfEachUseCase_RequiresItsOwnPermission(string method, string route, string permission)
    {
        Assert.Equal(permission, RoutePermissions.Of(factory.Services, method, route));
    }

    [Theory]
    [InlineData(JobsPermissions.Read)]
    [InlineData(JobsPermissions.Rerun)]
    [InlineData(JobsPermissions.Cancel)]
    public void PermissionsOf_EveryRoleButAdministrator_LeaveTheJobsPermissionToTheAdministrator(string permission)
    {
        var catalog = factory.Services.GetRequiredService<PermissionCatalog>();
        var others = Enum.GetValues<ErpRole>().Where(role => role != ErpRole.Administrator).ToList();

        Assert.True(catalog.Contains(permission));
        Assert.Contains(permission, catalog.PermissionsOf([ErpRole.Administrator]));
        Assert.NotEmpty(others);
        Assert.All(others, role => Assert.DoesNotContain(permission, catalog.PermissionsOf([role])));
        Assert.DoesNotContain(permission, catalog.PermissionsOf(others));
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/runs")]
    [InlineData("GET", "/runs/{id}")]
    [InlineData("POST", "/runs/{id}/rerun")]
    [InlineData("POST", "/runs/{id}/cancel")]
    public async Task Request_AsAPersonWithoutThePermission_AnswersForbidden(string method, string route)
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);
        using var request = new HttpRequestMessage(new HttpMethod(method), JobsApi.RouteFor(route));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await JobsApi.AssertProblemAsync(response, HttpStatusCode.Forbidden, "request.forbidden");
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/runs")]
    [InlineData("GET", "/runs/{id}")]
    [InlineData("POST", "/runs/{id}/rerun")]
    [InlineData("POST", "/runs/{id}/cancel")]
    public async Task Request_Anonymous_AnswersUnauthenticated(string method, string route)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), JobsApi.RouteFor(route));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
