using Dewiride.Erp.Modules.Identity.Users.People.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.UnitTests.People.Domain;

public sealed class UserTests
{
    private static readonly Guid ObjectId = new("6b1f2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d");

    private static readonly DateTimeOffset SignedInAt = new(2026, 10, 10, 5, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Register_ValidDetails_CreatesAnActiveRecordWithTrimmedAndNormalisedValues()
    {
        var result = User.Register(null, "  Meera Nair ", " Meera.Nair@Dewiride.com ", " DW-0042 ", "98765 43210", " Accountant ", new DateOnly(2026, 4, 1));

        Assert.True(result.IsSuccess);
        var user = result.Value;
        Assert.Equal(7, user.Id.Value.Version);
        Assert.Null(user.EntraObjectId);
        Assert.Equal("Meera Nair", user.DisplayName);
        Assert.Equal("meera.nair@dewiride.com", user.WorkEmail);
        Assert.Equal("DW-0042", user.EmployeeCode);
        Assert.Equal("+919876543210", user.PhoneNumber);
        Assert.Equal("Accountant", user.Designation);
        Assert.Equal(new DateOnly(2026, 4, 1), user.DateOfJoining);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.True(user.IsActive);
        Assert.Null(user.LastSignedInAt);
    }

    [Fact]
    public void Register_EntraObjectId_KeepsIt()
    {
        var result = User.Register(ObjectId, "Meera Nair", "meera.nair@dewiride.com", null, null, null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(ObjectId, result.Value.EntraObjectId);
        Assert.Null(result.Value.EmployeeCode);
        Assert.Null(result.Value.PhoneNumber);
        Assert.Null(result.Value.Designation);
        Assert.Null(result.Value.DateOfJoining);
    }

    [Theory]
    [InlineData("", "meera.nair@dewiride.com", "user.display-name-required", "DisplayName")]
    [InlineData("   ", "meera.nair@dewiride.com", "user.display-name-required", "DisplayName")]
    [InlineData("Meera Nair", "meera.nair", "user.work-email-invalid", "WorkEmail")]
    [InlineData("Meera Nair", "Meera Nair <meera.nair@dewiride.com>", "user.work-email-invalid", "WorkEmail")]
    [InlineData("Meera Nair", "", "user.work-email-invalid", "WorkEmail")]
    public void Register_InvalidNameOrEmail_FailsWithTheCodeOfThatMember(string displayName, string workEmail, string expectedCode, string expectedMember)
    {
        var result = User.Register(null, displayName, workEmail, null, null, null, null);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.Equal(expectedMember, Assert.Single(result.Error.Fields.Keys));
    }

    [Fact]
    public void Register_DisplayNameLongerThanTheLimit_FailsWithDisplayNameTooLong()
    {
        var result = User.Register(null, new string('a', User.DisplayNameMaxLength + 1), "meera.nair@dewiride.com", null, null, null, null);

        Assert.True(result.IsFailure);
        Assert.Equal("user.display-name-too-long", result.Error!.Code);
    }

    [Fact]
    public void Register_WorkEmailLongerThanTheLimit_FailsWithWorkEmailInvalid()
    {
        var result = User.Register(null, "Meera Nair", new string('a', User.WorkEmailMaxLength - "@dewiride.com".Length + 1) + "@dewiride.com", null, null, null, null);

        Assert.True(result.IsFailure);
        Assert.Equal("user.work-email-invalid", result.Error!.Code);
    }

    [Theory]
    [InlineData("98765 43210", "+919876543210")]
    [InlineData("098765-43210", "+919876543210")]
    [InlineData("6123456789", "+916123456789")]
    [InlineData("+91 98765 43210", "+919876543210")]
    [InlineData("+44 (20) 7946 0958", "+442079460958")]
    [InlineData("+1 415 555 0132", "+14155550132")]
    public void Describe_PhoneNumber_StoresItInInternationalForm(string phoneNumber, string expected)
    {
        var user = User.Register(null, "Meera Nair", "meera.nair@dewiride.com", null, null, null, null).Value;

        var result = user.Describe(null, phoneNumber, null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, user.PhoneNumber);
    }

    [Theory]
    [InlineData("5876543210")]
    [InlineData("987654321")]
    [InlineData("98765432101")]
    [InlineData("+0 123 456 789")]
    [InlineData("+1234567")]
    [InlineData("+1234567890123456")]
    [InlineData("+91 98765+43210")]
    [InlineData("98765.43210")]
    [InlineData("phone 9876543210")]
    public void Describe_PhoneNumberThatIsNoNumber_FailsWithPhoneNumberInvalid(string phoneNumber)
    {
        var user = User.Register(null, "Meera Nair", "meera.nair@dewiride.com", null, "9876543210", null, null).Value;

        var result = user.Describe(null, phoneNumber, null, null);

        Assert.True(result.IsFailure);
        Assert.Equal("user.phone-number-invalid", result.Error!.Code);
        Assert.Equal("PhoneNumber", Assert.Single(result.Error.Fields.Keys));
        Assert.Equal("+919876543210", user.PhoneNumber);
    }

    [Theory]
    [InlineData("-DW42")]
    [InlineData("DW 42")]
    [InlineData("DW.42")]
    [InlineData("ÉMP-1")]
    public void Describe_EmployeeCodeOutsideItsCharacters_FailsWithEmployeeCodeInvalid(string employeeCode)
    {
        var user = User.Register(null, "Meera Nair", "meera.nair@dewiride.com", null, null, null, null).Value;

        var result = user.Describe(employeeCode, null, null, null);

        Assert.True(result.IsFailure);
        Assert.Equal("user.employee-code-invalid", result.Error!.Code);
    }

    [Fact]
    public void Describe_EmployeeCodeLongerThanTheLimit_FailsWithEmployeeCodeInvalid()
    {
        var user = User.Register(null, "Meera Nair", "meera.nair@dewiride.com", null, null, null, null).Value;

        var result = user.Describe(new string('A', User.EmployeeCodeMaxLength + 1), null, null, null);

        Assert.True(result.IsFailure);
        Assert.Equal("user.employee-code-invalid", result.Error!.Code);
    }

    [Theory]
    [InlineData("DW/2026/042")]
    [InlineData("emp_42")]
    [InlineData("7")]
    public void Describe_EmployeeCodeOfItsCharacters_KeepsIt(string employeeCode)
    {
        var user = User.Register(null, "Meera Nair", "meera.nair@dewiride.com", null, null, null, null).Value;

        var result = user.Describe(employeeCode, null, null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(employeeCode, user.EmployeeCode);
    }

    [Fact]
    public void Describe_DesignationLongerThanTheLimit_FailsWithDesignationTooLong()
    {
        var user = User.Register(null, "Meera Nair", "meera.nair@dewiride.com", null, null, null, null).Value;

        var result = user.Describe(null, null, new string('a', User.DesignationMaxLength + 1), null);

        Assert.True(result.IsFailure);
        Assert.Equal("user.designation-too-long", result.Error!.Code);
    }

    [Fact]
    public void Describe_BlankValues_ClearEveryDetail()
    {
        var user = User.Register(null, "Meera Nair", "meera.nair@dewiride.com", "DW-0042", "9876543210", "Accountant", new DateOnly(2026, 4, 1)).Value;

        var result = user.Describe(" ", "", null, null);

        Assert.True(result.IsSuccess);
        Assert.Null(user.EmployeeCode);
        Assert.Null(user.PhoneNumber);
        Assert.Null(user.Designation);
        Assert.Null(user.DateOfJoining);
    }

    [Fact]
    public void FirstSignIn_ValidClaims_CreatesAnActiveRecordLinkedToTheAccount()
    {
        var result = User.FirstSignIn(ObjectId, "Meera Nair", "Meera.Nair@Dewiride.com", SignedInAt);

        Assert.True(result.IsSuccess);
        Assert.Equal(ObjectId, result.Value.EntraObjectId);
        Assert.Equal("Meera Nair", result.Value.DisplayName);
        Assert.Equal("meera.nair@dewiride.com", result.Value.WorkEmail);
        Assert.Equal(SignedInAt, result.Value.LastSignedInAt);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public void FirstSignIn_ClaimsWithoutAName_FailsWithDisplayNameRequired()
    {
        var result = User.FirstSignIn(ObjectId, "", "meera.nair@dewiride.com", SignedInAt);

        Assert.True(result.IsFailure);
        Assert.Equal("user.display-name-required", result.Error!.Code);
    }

    [Fact]
    public void SignIn_RegisteredRecord_LinksTheAccountAndBringsTheNameAndEmail()
    {
        var user = User.Register(null, "Meera", "meera@dewiride.com", "DW-0042", null, null, null).Value;

        var result = user.SignIn(ObjectId, "Meera Nair", "meera.nair@dewiride.com", SignedInAt);

        Assert.True(result.IsSuccess);
        Assert.Equal(ObjectId, user.EntraObjectId);
        Assert.Equal("Meera Nair", user.DisplayName);
        Assert.Equal("meera.nair@dewiride.com", user.WorkEmail);
        Assert.Equal("DW-0042", user.EmployeeCode);
        Assert.Equal(SignedInAt, user.LastSignedInAt);
    }

    [Fact]
    public void SignIn_RecordOfAnotherAccount_FailsWithLinkedToAnotherAccountAndKeepsTheRecord()
    {
        var user = User.FirstSignIn(ObjectId, "Meera Nair", "meera.nair@dewiride.com", SignedInAt).Value;

        var result = user.SignIn(Guid.CreateVersion7(), "Someone Else", "someone@dewiride.com", SignedInAt.AddHours(1));

        Assert.True(result.IsFailure);
        Assert.Equal("user.linked-to-another-account", result.Error!.Code);
        Assert.Equal(ObjectId, user.EntraObjectId);
        Assert.Equal("Meera Nair", user.DisplayName);
        Assert.Equal(SignedInAt, user.LastSignedInAt);
    }

    [Fact]
    public void SignIn_ClaimsWithAnInvalidEmail_FailsAndKeepsTheSignInTime()
    {
        var user = User.FirstSignIn(ObjectId, "Meera Nair", "meera.nair@dewiride.com", SignedInAt).Value;

        var result = user.SignIn(ObjectId, "Meera Nair", "not an email", SignedInAt.AddHours(1));

        Assert.True(result.IsFailure);
        Assert.Equal("user.work-email-invalid", result.Error!.Code);
        Assert.Equal(SignedInAt, user.LastSignedInAt);
    }

    [Fact]
    public void Deactivate_ThenReactivate_ChangesWhetherThePersonIsActive()
    {
        var user = User.FirstSignIn(ObjectId, "Meera Nair", "meera.nair@dewiride.com", SignedInAt).Value;

        user.Deactivate();
        Assert.Equal(UserStatus.Deactivated, user.Status);
        Assert.False(user.IsActive);

        user.Reactivate();
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void NormaliseWorkEmail_MixedCaseWithSpaces_ReturnsItTrimmedInLowerCase()
    {
        Assert.Equal("meera.nair@dewiride.com", User.NormaliseWorkEmail("  Meera.NAIR@Dewiride.com "));
    }
}
