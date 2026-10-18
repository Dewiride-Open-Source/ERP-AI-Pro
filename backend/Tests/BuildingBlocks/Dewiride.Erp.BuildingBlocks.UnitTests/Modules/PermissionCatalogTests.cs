using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Modules;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Modules;

public sealed class PermissionCatalogTests
{
    private const string ReadInvoices = "finance.sales.invoices.read";

    private const string IssueInvoices = "finance.sales.invoices.issue";

    private const string VoidInvoices = "finance.sales.invoices.void";

    private const string ReadGstr2b = "platform.gst.gstr-2b.read";

    private const string ReadInvoicesDescription = "See the sales invoices.";

    private const string IssueInvoicesDescription = "Issue a sales invoice.";

    private const string VoidInvoicesDescription = "Void an issued sales invoice.";

    private const string ReadGstr2bDescription = "See the GSTR-2B statements.";

    private static readonly PermissionCatalog Catalog = Of(
        new StubModule("Finance", "Sales")
        {
            Permissions =
            [
                new(ReadInvoices, [ErpRole.Finance, ErpRole.Employee], ReadInvoicesDescription),
                new(IssueInvoices, [ErpRole.Finance], IssueInvoicesDescription),
                new(VoidInvoices, [], VoidInvoicesDescription),
            ],
        },
        new StubModule("Platform", "Gst") { Permissions = [new(ReadGstr2b, [ErpRole.Finance], ReadGstr2bDescription)] });

    [Fact]
    public void Names_DeclaredByEveryModule_ListsThemInOrdinalOrder()
    {
        Assert.Equal([IssueInvoices, ReadInvoices, VoidInvoices, ReadGstr2b], Catalog.Names);
    }

    [Fact]
    public void Permissions_DeclaredByEveryModule_ListsThemInOrdinalOrderWithTheirDescriptions()
    {
        Assert.Equal(
            [
                (IssueInvoices, IssueInvoicesDescription),
                (ReadInvoices, ReadInvoicesDescription),
                (VoidInvoices, VoidInvoicesDescription),
                (ReadGstr2b, ReadGstr2bDescription),
            ],
            Catalog.Permissions.Select(permission => (permission.Name, permission.Description)));
    }

    [Fact]
    public void Permissions_RolesDeclaredOutOfOrder_AreHeldInRoleOrderWithAdministratorLast()
    {
        Assert.Equal([ErpRole.Employee, ErpRole.Finance, ErpRole.Administrator], Single(ReadInvoices).HeldBy);
    }

    [Fact]
    public void Permissions_NoRoleDeclared_AreHeldByAdministratorAlone()
    {
        Assert.Equal([ErpRole.Administrator], Single(VoidInvoices).HeldBy);
    }

    [Fact]
    public void Permissions_EveryPermission_IsHeldByTheRolesWhosePermissionsHoldIt()
    {
        foreach (var permission in Catalog.Permissions)
        {
            Assert.Equal(
                Enum.GetValues<ErpRole>().Where(role => Catalog.PermissionsOf([role]).Contains(permission.Name)),
                permission.HeldBy);
        }
    }

    [Fact]
    public void Contains_DeclaredAndUndeclaredNames_TellsThemApart()
    {
        Assert.True(Catalog.Contains(VoidInvoices));
        Assert.False(Catalog.Contains("finance.sales.invoices.delete"));
    }

    [Fact]
    public void PermissionsOf_Administrator_HoldsEveryPermission()
    {
        Assert.Equal(Catalog.Names, Catalog.PermissionsOf([ErpRole.Administrator]).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void PermissionsOf_Employee_HoldsOnlyThePermissionsThatNameIt()
    {
        Assert.Equal([ReadInvoices], Catalog.PermissionsOf([ErpRole.Employee]));
    }

    [Fact]
    public void PermissionsOf_SeveralRoles_HoldsTheUnionOfTheirPermissions()
    {
        Assert.Equal([IssueInvoices, ReadInvoices, ReadGstr2b], Catalog.PermissionsOf([ErpRole.Employee, ErpRole.Finance]).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void PermissionsOf_NoRole_HoldsNone()
    {
        Assert.Empty(Catalog.PermissionsOf([]));
    }

    [Theory]
    [InlineData("finance.sales.invoices")]
    [InlineData("finance.sales.invoices.issue.now")]
    [InlineData("finance.sales.Invoices.issue")]
    [InlineData("finance.sales.invoices.issue_now")]
    [InlineData("finance.sales..issue")]
    public void Constructor_NameThatIsNotFourLowerCaseSegments_Throws(string name)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Of(new StubModule("Finance", "Sales") { Permissions = [new(name, [], IssueInvoicesDescription)] }));

        Assert.Contains($"'{name}', which is not four lower-case segments", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_PermissionHeldByAdministrator_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Of(new StubModule("Finance", "Sales") { Permissions = [new(IssueInvoices, [ErpRole.Administrator], IssueInvoicesDescription)] }));

        Assert.Contains("which holds every permission", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n")]
    public void Constructor_PermissionWithoutADescription_ThrowsNamingThePermission(string description)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Of(new StubModule("Finance", "Sales") { Permissions = [new(IssueInvoices, [ErpRole.Finance], description)] }));

        Assert.Contains($"'Finance.Sales' declares the permission '{IssueInvoices}' without a description", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_PermissionDeclaredTwice_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Of(new StubModule("Finance", "Sales")
        {
            Permissions = [new(IssueInvoices, [], IssueInvoicesDescription), new(IssueInvoices, [ErpRole.Finance], IssueInvoicesDescription)],
        }));

        Assert.Contains($"'{IssueInvoices}' is declared more than once", exception.Message, StringComparison.Ordinal);
    }

    private static PermissionCatalogEntry Single(string name) => Assert.Single(Catalog.Permissions, permission => permission.Name == name);

    private static PermissionCatalog Of(params IModule[] modules) => new(new ModuleCatalog(modules));
}
