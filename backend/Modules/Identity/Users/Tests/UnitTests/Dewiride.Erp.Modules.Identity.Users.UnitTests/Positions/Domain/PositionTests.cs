using Dewiride.Erp.Modules.Identity.Users.Departments.Domain;
using Dewiride.Erp.Modules.Identity.Users.Designations.Domain;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Positions.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.UnitTests.Positions.Domain;

public sealed class PositionTests
{
    private static readonly DateOnly April = new(2026, 4, 1);

    [Fact]
    public void Record_DepartmentDesignationAndManager_HoldsThemFromTheDate()
    {
        var person = UserId.Create();
        var department = DepartmentId.Create();
        var designation = DesignationId.Create();
        var manager = UserId.Create();

        var result = Position.Record(person, April, department, designation, manager);

        Assert.True(result.IsSuccess);
        Assert.Equal(person, result.Value.UserId);
        Assert.Equal(April, result.Value.EffectiveFrom);
        Assert.Equal(department, result.Value.DepartmentId);
        Assert.Equal(designation, result.Value.DesignationId);
        Assert.Equal(manager, result.Value.ManagerId);
    }

    [Fact]
    public void Record_NothingNamed_HoldsAnEmptyPositionFromTheDate()
    {
        var result = Position.Record(UserId.Create(), April, null, null, null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.DepartmentId);
        Assert.Null(result.Value.DesignationId);
        Assert.Null(result.Value.ManagerId);
    }

    [Fact]
    public void Record_ThePersonAsTheirOwnManager_FailsWithSelfManaged()
    {
        var person = UserId.Create();

        var result = Position.Record(person, April, null, null, person);

        Assert.True(result.IsFailure);
        Assert.Equal("position.self-managed", result.Error!.Code);
    }
}
