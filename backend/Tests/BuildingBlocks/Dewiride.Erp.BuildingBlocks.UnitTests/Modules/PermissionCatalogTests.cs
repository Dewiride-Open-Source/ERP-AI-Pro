using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Modules;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Modules;

public sealed class PermissionCatalogTests
{
    private const string ReadInvoices = "finance.sales.invoices.read";

    private const string IssueInvoices = "finance.sales.invoices.issue";

    private const string VoidInvoices = "finance.sales.invoices.void";

    private const string ReadGstr2b = "platform.gst.gstr-2b.read";

    private static readonly PermissionCatalog Catalog = Of(
        new StubModule("Finance", "Sales")
        {
            Permissions =
            [
                new(ReadInvoices, [ErpRole.Employee, ErpRole.Finance]),
                new(IssueInvoices, [ErpRole.Finance]),
                new(VoidInvoices, []),
            ],
        },
        new StubModule("Platform", "Gst") { Permissions = [new(ReadGstr2b, [ErpRole.Finance])] });

    [Fact]
    public void Names_DeclaredByEveryModule_ListsThemInOrdinalOrder()
    {
        Assert.Equal([IssueInvoices, ReadInvoices, VoidInvoices, ReadGstr2b], Catalog.Names);
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
        var exception = Assert.Throws<InvalidOperationException>(() => Of(new StubModule("Finance", "Sales") { Permissions = [new(name, [])] }));

        Assert.Contains($"'{name}', which is not four lower-case segments", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_PermissionHeldByAdministrator_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Of(new StubModule("Finance", "Sales") { Permissions = [new(IssueInvoices, [ErpRole.Administrator])] }));

        Assert.Contains("which holds every permission", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_PermissionDeclaredTwice_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Of(new StubModule("Finance", "Sales") { Permissions = [new(IssueInvoices, []), new(IssueInvoices, [ErpRole.Finance])] }));

        Assert.Contains($"'{IssueInvoices}' is declared more than once", exception.Message, StringComparison.Ordinal);
    }

    private static PermissionCatalog Of(params IModule[] modules) => new(new ModuleCatalog(modules));
}
