using Dewiride.Erp.Modules.Identity.Users.Departments.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.UnitTests.Departments.Domain;

public sealed class DepartmentTests
{
    [Fact]
    public void Create_NameWithSpacesAround_KeepsItTrimmedUnderTheParent()
    {
        var parent = DepartmentId.Create();

        var result = Department.Create("  Finance ", parent);

        Assert.True(result.IsSuccess);
        Assert.Equal("Finance", result.Value.Name);
        Assert.Equal(parent, result.Value.ParentId);
        Assert.Equal(7, result.Value.Id.Value.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_FailsWithNameRequired(string name)
    {
        var result = Department.Create(name, null);

        Assert.True(result.IsFailure);
        Assert.Equal("department.name-required", result.Error!.Code);
        Assert.Equal(nameof(Department.Name), Assert.Single(result.Error.Fields.Keys));
    }

    [Fact]
    public void Create_NameLongerThanTheLimit_FailsWithNameTooLong()
    {
        var result = Department.Create(new string('a', Department.NameMaxLength + 1), null);

        Assert.True(result.IsFailure);
        Assert.Equal("department.name-too-long", result.Error!.Code);
    }

    [Fact]
    public void MoveUnder_ADepartmentOutsideItsOwnBranch_ChangesTheParent()
    {
        var finance = Department.Create("Finance", null).Value;
        var engineering = Department.Create("Engineering", null).Value;
        var tree = Tree(finance, engineering);

        var result = finance.MoveUnder(engineering.Id, tree);

        Assert.True(result.IsSuccess);
        Assert.Equal(engineering.Id, finance.ParentId);
    }

    [Fact]
    public void MoveUnder_Itself_FailsWithWouldContainItself()
    {
        var finance = Department.Create("Finance", null).Value;

        var result = finance.MoveUnder(finance.Id, Tree(finance));

        Assert.True(result.IsFailure);
        Assert.Equal("department.would-contain-itself", result.Error!.Code);
        Assert.Null(finance.ParentId);
    }

    [Fact]
    public void MoveUnder_ADepartmentWithinIt_FailsWithWouldContainItself()
    {
        var finance = Department.Create("Finance", null).Value;
        var payables = Department.Create("Payables", finance.Id).Value;
        var invoices = Department.Create("Invoices", payables.Id).Value;

        var result = finance.MoveUnder(invoices.Id, Tree(finance, payables, invoices));

        Assert.True(result.IsFailure);
        Assert.Equal("department.would-contain-itself", result.Error!.Code);
    }

    [Fact]
    public void MoveUnder_NoParent_MovesItToTheTop()
    {
        var finance = Department.Create("Finance", null).Value;
        var payables = Department.Create("Payables", finance.Id).Value;

        var result = payables.MoveUnder(null, Tree(finance, payables));

        Assert.True(result.IsSuccess);
        Assert.Null(payables.ParentId);
    }

    [Fact]
    public void IsWithin_ParentsThatLoop_StopsInsteadOfLooping()
    {
        var first = DepartmentId.Create();
        var second = DepartmentId.Create();
        var tree = new DepartmentTree(new Dictionary<DepartmentId, DepartmentId?> { [first] = second, [second] = first });

        Assert.False(tree.IsWithin(first, DepartmentId.Create()));
        Assert.True(tree.IsWithin(first, second));
    }

    private static DepartmentTree Tree(params Department[] departments) =>
        new(departments.ToDictionary(department => department.Id, department => department.ParentId));
}
