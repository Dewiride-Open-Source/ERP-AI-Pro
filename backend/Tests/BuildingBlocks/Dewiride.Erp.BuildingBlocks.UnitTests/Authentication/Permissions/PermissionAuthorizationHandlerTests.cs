using System.Collections.Frozen;
using System.Security.Claims;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Authentication.Permissions;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.BuildingBlocks.UnitTests.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.Permissions;

public sealed class PermissionAuthorizationHandlerTests
{
    private const string ReadInvoices = "finance.sales.invoices.read";

    private const string IssueInvoices = "finance.sales.invoices.issue";

    private const string VoidInvoices = "finance.sales.invoices.void";

    private static readonly Guid ObjectId = Guid.Parse("b47e2c91-5d3a-4e8f-9c6b-2a1f0e9d8c73");

    private static readonly PermissionCatalog Catalog = new(new ModuleCatalog(
    [
        new StubModule("Finance", "Sales")
        {
            Permissions =
            [
                new(ReadInvoices, [ErpRole.Employee], "See the sales invoices."),
                new(IssueInvoices, [ErpRole.Finance], "Issue a sales invoice."),
                new(VoidInvoices, [], "Void an issued sales invoice."),
            ],
        },
    ]));

    [Fact]
    public async Task HandleAsync_PersonWithTheUserAppRole_HoldsTheEmployeePermissionsOnly()
    {
        var person = Person(AppRoles.User);

        Assert.True(await IsGrantedAsync(person, ReadInvoices));
        Assert.False(await IsGrantedAsync(person, IssueInvoices));
    }

    [Fact]
    public async Task HandleAsync_PersonWithTheAdministratorAppRole_HoldsEveryPermission()
    {
        Assert.True(await IsGrantedAsync(Person(AppRoles.Administrator), VoidInvoices));
    }

    [Fact]
    public async Task HandleAsync_PersonWhoseRecordWasAssignedARole_HoldsItsPermissions()
    {
        var request = Request(Person(AppRoles.User));
        request.Features.Set(new PersonRolesFeature(ObjectId, new[] { ErpRole.Finance }.ToFrozenSet()));

        Assert.True(await IsGrantedAsync(request, IssueInvoices));
        Assert.False(await IsGrantedAsync(request, VoidInvoices));
    }

    [Fact]
    public async Task HandleAsync_RolesAssignedToAnotherPerson_DoNotCount()
    {
        var request = Request(Person(AppRoles.User));
        request.Features.Set(new PersonRolesFeature(Guid.CreateVersion7(), new[] { ErpRole.Finance }.ToFrozenSet()));

        Assert.False(await IsGrantedAsync(request, IssueInvoices));
    }

    [Fact]
    public async Task HandleAsync_RecordNamingTheRolesOnlyAppRolesGive_GrantsNeither()
    {
        var request = Request(Person());
        request.Features.Set(new PersonRolesFeature(ObjectId, new[] { ErpRole.Administrator, ErpRole.Employee }.ToFrozenSet()));

        Assert.False(await IsGrantedAsync(request, VoidInvoices));
        Assert.False(await IsGrantedAsync(request, ReadInvoices));
    }

    [Fact]
    public async Task HandleAsync_CallerNobodySignedIn_HoldsNothingAndIsMarkedRefused()
    {
        var request = Request(new ClaimsPrincipal(new ClaimsIdentity([new Claim("oid", ObjectId.ToString()), new Claim("roles", AppRoles.Administrator)])));

        Assert.False(await IsGrantedAsync(request, ReadInvoices));
        Assert.True(PermissionRefusals.IsMarked(request));
    }

    [Fact]
    public async Task HandleAsync_ApplicationWhoseRolesNameAPersonsAppRole_HoldsNothing()
    {
        var application = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("idtyp", "app"), new Claim("oid", ObjectId.ToString()), new Claim("roles", AppRoles.Administrator)],
            "Bearer"));

        Assert.False(await IsGrantedAsync(application, ReadInvoices));
    }

    [Fact]
    public async Task HandleAsync_PermissionNoModuleDeclares_Throws()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => IsGrantedAsync(Person(AppRoles.Administrator), "finance.sales.invoices.delete"));

        Assert.Contains("which no module declares", exception.Message, StringComparison.Ordinal);
    }

    private static ClaimsPrincipal Person(params string[] appRoles) =>
        new(new ClaimsIdentity([new Claim("oid", ObjectId.ToString()), .. appRoles.Select(role => new Claim("roles", role))], "Cookies"));

    private static DefaultHttpContext Request(ClaimsPrincipal caller) => new() { User = caller };

    private static Task<bool> IsGrantedAsync(ClaimsPrincipal caller, string permission) => IsGrantedAsync(Request(caller), permission);

    private static async Task<bool> IsGrantedAsync(HttpContext request, string permission)
    {
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], request.User, request);

        await new PermissionAuthorizationHandler(Catalog).HandleAsync(context);

        return context.HasSucceeded;
    }
}
