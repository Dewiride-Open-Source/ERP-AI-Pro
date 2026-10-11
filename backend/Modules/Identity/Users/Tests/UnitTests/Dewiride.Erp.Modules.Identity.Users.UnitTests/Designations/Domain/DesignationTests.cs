using Dewiride.Erp.Modules.Identity.Users.Designations.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.UnitTests.Designations.Domain;

public sealed class DesignationTests
{
    [Fact]
    public void Create_NameWithSpacesAround_KeepsItTrimmed()
    {
        var result = Designation.Create(" Senior Accountant  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Senior Accountant", result.Value.Name);
        Assert.Equal(7, result.Value.Id.Value.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_BlankName_FailsWithNameRequired(string name)
    {
        var result = Designation.Create(name);

        Assert.True(result.IsFailure);
        Assert.Equal("designation.name-required", result.Error!.Code);
        Assert.Equal(nameof(Designation.Name), Assert.Single(result.Error.Fields.Keys));
    }

    [Fact]
    public void Rename_NameLongerThanTheLimit_FailsWithNameTooLongAndKeepsTheName()
    {
        var designation = Designation.Create("Accountant").Value;

        var result = designation.Rename(new string('a', Designation.NameMaxLength + 1));

        Assert.True(result.IsFailure);
        Assert.Equal("designation.name-too-long", result.Error!.Code);
        Assert.Equal("Accountant", designation.Name);
    }
}
