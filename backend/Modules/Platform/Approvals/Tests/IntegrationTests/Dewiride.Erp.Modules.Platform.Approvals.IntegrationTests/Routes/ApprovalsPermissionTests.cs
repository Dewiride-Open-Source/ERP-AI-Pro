using System.Net;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.Modules.Platform.Approvals.Contracts;
using Dewiride.Erp.Modules.Platform.Approvals.IntegrationTests.Shared;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authorization;

namespace Dewiride.Erp.Modules.Platform.Approvals.IntegrationTests.Routes;

public sealed class ApprovalsPermissionTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    public static TheoryData<string, string> EveryRoute => new()
    {
        { "GET", "/requests" },
        { "GET", "/requests/counts" },
        { "GET", "/requests/{id}" },
        { "POST", "/requests/{id}/approve" },
        { "POST", "/requests/{id}/return" },
        { "POST", "/requests/{id}/reject" },
        { "POST", "/requests/{id}/withdraw" },
        { "GET", "/kinds" },
        { "POST", "/kind-settings" },
        { "PUT", "/kind-settings/{id}" },
    };

    [Theory]
    [InlineData("GET", "/api/platform/approvals/requests", ApprovalsPermissions.Read)]
    [InlineData("GET", "/api/platform/approvals/requests/counts", ApprovalsPermissions.Read)]
    [InlineData("GET", "/api/platform/approvals/requests/{id:guid}", ApprovalsPermissions.Read)]
    [InlineData("POST", "/api/platform/approvals/requests/{id:guid}/approve", ApprovalsPermissions.Decide)]
    [InlineData("POST", "/api/platform/approvals/requests/{id:guid}/return", ApprovalsPermissions.Decide)]
    [InlineData("POST", "/api/platform/approvals/requests/{id:guid}/reject", ApprovalsPermissions.Decide)]
    [InlineData("POST", "/api/platform/approvals/requests/{id:guid}/withdraw", ApprovalsPermissions.Withdraw)]
    [InlineData("GET", "/api/platform/approvals/kinds", ApprovalsPermissions.Read)]
    [InlineData("POST", "/api/platform/approvals/kind-settings", ApprovalsPermissions.ChangeKinds)]
    [InlineData("PUT", "/api/platform/approvals/kind-settings/{id:guid}", ApprovalsPermissions.ChangeKinds)]
    public void Route_OfEachUseCase_RequiresItsOwnPermission(string method, string route, string permission)
    {
        Assert.Equal(permission, RoutePermissions.Of(factory.Services, method, route));
    }

    [Fact]
    public void Descriptor_DeclaresTheSixPermissionsOfApprovals()
    {
        var module = new ApprovalsModule();

        Assert.Equal(
            [ApprovalsPermissions.Read, ApprovalsPermissions.Decide, ApprovalsPermissions.Withdraw, ApprovalsPermissions.ReadAll, ApprovalsPermissions.DecideAny, ApprovalsPermissions.ChangeKinds],
            module.Descriptor.Permissions.Select(permission => permission.Name));
    }

    [Theory]
    [InlineData(ApprovalsPermissions.Read)]
    [InlineData(ApprovalsPermissions.Decide)]
    [InlineData(ApprovalsPermissions.Withdraw)]
    public void PermissionsOf_Roles_GiveEveryEmployeeTheirOwnRequests(string permission)
    {
        var catalog = factory.Services.GetRequiredService<PermissionCatalog>();

        Assert.Contains(permission, catalog.PermissionsOf([ErpRole.Employee]));
        Assert.Contains(permission, catalog.PermissionsOf([ErpRole.Administrator]));
    }

    [Theory]
    [InlineData(ApprovalsPermissions.ReadAll)]
    [InlineData(ApprovalsPermissions.DecideAny)]
    [InlineData(ApprovalsPermissions.ChangeKinds)]
    public void PermissionsOf_Roles_GiveEveryRequestAndTheKindsSettingsToAdministratorsAlone(string permission)
    {
        var catalog = factory.Services.GetRequiredService<PermissionCatalog>();

        Assert.Contains(permission, catalog.PermissionsOf([ErpRole.Administrator]));
        Assert.DoesNotContain(permission, catalog.PermissionsOf([ErpRole.Employee, ErpRole.Finance, ErpRole.Hr, ErpRole.Manager]));
    }

    [Theory]
    [InlineData("POST", "/kind-settings")]
    [InlineData("PUT", "/kind-settings/{id}")]
    public async Task Request_AsAnEmployeeWithoutThePermission_AnswersForbidden(string method, string route)
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);
        using var request = new HttpRequestMessage(new HttpMethod(method), ApprovalsApi.RouteFor(route));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await ApprovalsApi.AssertProblemAsync(response, HttpStatusCode.Forbidden, "request.forbidden");
    }

    [Theory]
    [MemberData(nameof(EveryRoute))]
    public async Task Request_Anonymous_AnswersUnauthenticated(string method, string route)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), ApprovalsApi.RouteFor(route));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
