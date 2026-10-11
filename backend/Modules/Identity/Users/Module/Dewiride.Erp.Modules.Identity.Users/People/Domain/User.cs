using System.Net.Mail;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Identity.Users.People.Domain;

// Entra keeps a person's name and work email, so every sign-in brings them up to date; the employee code, phone number,
// date of joining and the roles assigned in the ERP are kept here by administrators, and the person's department,
// designation and manager by their dated positions. A person holds
// Administrator and Employee through the app roles of their Entra account, so only Finance, HR and Manager are assigned
// here. A record an administrator registers by work email is linked to the person's Entra account at their first sign-in;
// one invited from the company directory is linked from the start.
internal sealed class User : AggregateRoot<UserId>, IAuditable, ISoftDeletable, IVersioned
{
    public const int DisplayNameMaxLength = 256;

    public const int WorkEmailMaxLength = 254;

    public const int EmployeeCodeMaxLength = 32;

    // ITU-T Recommendation E.164 allows at most 15 digits after the plus sign.
    public const int PhoneNumberMaxLength = 16;

    private const string IndianCountryCode = "91";

    private List<ErpRole> _roles = [];

    private User(UserId id)
        : base(id)
    {
    }

    public Guid? EntraObjectId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string WorkEmail { get; private set; } = string.Empty;

    public string? EmployeeCode { get; private set; }

    public string? PhoneNumber { get; private set; }

    public DateOnly? DateOfJoining { get; private set; }

    public UserStatus Status { get; private set; }

    public IReadOnlyList<ErpRole> Roles => _roles;

    public DateTimeOffset? LastSignedInAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive => Status == UserStatus.Active;

    public static Result<User> Register(
        string displayName,
        string workEmail,
        string? employeeCode,
        string? phoneNumber,
        DateOnly? dateOfJoining,
        IReadOnlyCollection<ErpRole> roles) =>
        Create(null, displayName, workEmail, employeeCode, phoneNumber, dateOfJoining, roles);

    public static Result<User> Invite(
        Guid entraObjectId,
        string displayName,
        string workEmail,
        string? employeeCode,
        string? phoneNumber,
        DateOnly? dateOfJoining,
        IReadOnlyCollection<ErpRole> roles) =>
        Create(entraObjectId, displayName, workEmail, employeeCode, phoneNumber, dateOfJoining, roles);

    public static Result<User> FirstSignIn(Guid entraObjectId, string displayName, string workEmail, DateTimeOffset signedInAt)
    {
        var user = new User(UserId.Create()) { Status = UserStatus.Active };
        var signedIn = user.SignIn(entraObjectId, displayName, workEmail, signedInAt);

        return signedIn.IsFailure ? signedIn.Error! : user;
    }

    public static bool IsAssignable(ErpRole role) => role is ErpRole.Finance or ErpRole.Hr or ErpRole.Manager;

    public static string NormaliseWorkEmail(string workEmail)
    {
        ArgumentNullException.ThrowIfNull(workEmail);

        return workEmail.Trim().ToLowerInvariant();
    }

    public Result SignIn(Guid entraObjectId, string displayName, string workEmail, DateTimeOffset signedInAt)
    {
        if (EntraObjectId is { } linked && linked != entraObjectId)
        {
            return UserErrors.LinkedToAnotherAccount;
        }

        var named = Name(displayName, workEmail);
        if (named.IsFailure)
        {
            return named;
        }

        EntraObjectId = entraObjectId;
        LastSignedInAt = signedInAt;

        return Result.Success();
    }

    public Result Describe(string? employeeCode, string? phoneNumber, DateOnly? dateOfJoining)
    {
        var code = Blank(employeeCode);
        if (code is not null && !IsEmployeeCode(code))
        {
            return UserErrors.EmployeeCodeInvalid;
        }

        string? phone = null;
        if (Blank(phoneNumber) is { } number && !TryNormalisePhoneNumber(number, out phone))
        {
            return UserErrors.PhoneNumberInvalid;
        }

        EmployeeCode = code;
        PhoneNumber = phone;
        DateOfJoining = dateOfJoining;

        return Result.Success();
    }

    public Result AssignRoles(IEnumerable<ErpRole> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);

        var assigned = roles.Distinct().Order().ToList();
        if (!assigned.TrueForAll(IsAssignable))
        {
            return UserErrors.RoleNotAssignable;
        }

        _roles = assigned;

        return Result.Success();
    }

    public void Deactivate() => Status = UserStatus.Deactivated;

    public void Reactivate() => Status = UserStatus.Active;

    private static Result<User> Create(
        Guid? entraObjectId,
        string displayName,
        string workEmail,
        string? employeeCode,
        string? phoneNumber,
        DateOnly? dateOfJoining,
        IReadOnlyCollection<ErpRole> roles)
    {
        var user = new User(UserId.Create()) { EntraObjectId = entraObjectId, Status = UserStatus.Active };
        var named = user.Name(displayName, workEmail);
        if (named.IsFailure)
        {
            return named.Error!;
        }

        var described = user.Describe(employeeCode, phoneNumber, dateOfJoining);
        if (described.IsFailure)
        {
            return described.Error!;
        }

        var assigned = user.AssignRoles(roles);

        return assigned.IsFailure ? assigned.Error! : user;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsEmployeeCode(string code) =>
        code.Length <= EmployeeCodeMaxLength
        && char.IsAsciiLetterOrDigit(code[0])
        && code.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '/' or '_');

    // A number written without its country code is read as an Indian mobile number: ten digits starting with 6, 7, 8 or 9,
    // with or without a leading 0, as the national numbering plan of India writes them.
    private static bool TryNormalisePhoneNumber(string value, out string? normalised)
    {
        normalised = null;
        var international = value.StartsWith('+');
        var digits = new string([.. value.Where(char.IsAsciiDigit)]);
        if (value.Any(c => !char.IsAsciiDigit(c) && c is not (' ' or '-' or '(' or ')') && !(c == '+' && international)) || value.Count(c => c == '+') > 1)
        {
            return false;
        }

        if (!international)
        {
            if (digits.Length == 11 && digits[0] == '0')
            {
                digits = digits[1..];
            }

            if (digits.Length != 10 || digits[0] is < '6' or > '9')
            {
                return false;
            }

            digits = IndianCountryCode + digits;
        }

        if (digits.Length is < 8 or > PhoneNumberMaxLength - 1 || digits[0] == '0')
        {
            return false;
        }

        normalised = "+" + digits;

        return true;
    }

    private Result Name(string displayName, string workEmail)
    {
        var name = displayName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return UserErrors.DisplayNameRequired;
        }

        if (name.Length > DisplayNameMaxLength)
        {
            return UserErrors.DisplayNameTooLong;
        }

        var email = NormaliseWorkEmail(workEmail ?? string.Empty);
        if (email.Length > WorkEmailMaxLength || !MailAddress.TryCreate(email, out var address) || !string.Equals(address.Address, email, StringComparison.Ordinal))
        {
            return UserErrors.WorkEmailInvalid;
        }

        DisplayName = name;
        WorkEmail = email;

        return Result.Success();
    }
}
