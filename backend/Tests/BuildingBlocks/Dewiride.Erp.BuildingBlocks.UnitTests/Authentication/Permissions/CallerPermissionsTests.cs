using System.Collections.Frozen;
using System.Security.Claims;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Authentication.Permissions;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.BuildingBlocks.UnitTests.Modules;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.Permissions;

public sealed class CallerPermissionsTests
{
    private const string ReadInvoices = "finance.sales.invoices.read";

    private const string IssueInvoices = "finance.sales.invoices.issue";

    private static readonly Guid ObjectId = Guid.Parse("5c2e8a41-9d3b-4f6a-8e7c-1b0d2f4a6c95");

    private static readonly PermissionCatalog Catalog = new(new ModuleCatalog(
    [
        new StubModule("Finance", "Sales")
        {
            Permissions =
            [
                new(ReadInvoices, [ErpRole.Employee]),
                new(IssueInvoices, [ErpRole.Finance]),
            ],
        },
    ]));

    [Fact]
    public void Holds_PersonWithTheUserAppRole_HoldsTheEmployeePermissionsOnly()
    {
        var permissions = PermissionsOf(Request(AppRoles.User));

        Assert.True(permissions.Holds(ReadInvoices));
        Assert.False(permissions.Holds(IssueInvoices));
    }

    [Fact]
    public void Holds_PersonWhoseRecordWasAssignedARole_HoldsItsPermissions()
    {
        var request = Request(AppRoles.User);
        request.Features.Set(new PersonRolesFeature(ObjectId, new[] { ErpRole.Finance }.ToFrozenSet()));

        Assert.True(PermissionsOf(request).Holds(IssueInvoices));
    }

    [Fact]
    public void Holds_RolesAssignedToAnotherPerson_DoNotCount()
    {
        var request = Request(AppRoles.User);
        request.Features.Set(new PersonRolesFeature(Guid.CreateVersion7(), new[] { ErpRole.Finance }.ToFrozenSet()));

        Assert.False(PermissionsOf(request).Holds(IssueInvoices));
    }

    [Fact]
    public void Holds_OutsideARequest_Throws()
    {
        var permissions = new CallerPermissions(new HttpContextAccessor(), Catalog);

        Assert.Throws<InvalidOperationException>(() => permissions.Holds(ReadInvoices));
    }

    private static DefaultHttpContext Request(params string[] appRoles) =>
        new() { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("oid", ObjectId.ToString()), .. appRoles.Select(role => new Claim("roles", role))], "Cookies")) };

    private static CallerPermissions PermissionsOf(HttpContext request) => new(new HttpContextAccessor { HttpContext = request }, Catalog);
}
