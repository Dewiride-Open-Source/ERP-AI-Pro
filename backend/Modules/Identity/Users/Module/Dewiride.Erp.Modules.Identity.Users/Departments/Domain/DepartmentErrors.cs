using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Domain;

internal static class DepartmentErrors
{
    public static readonly Error NameRequired = Field("department.name-required", nameof(Department.Name), "A department's name must not be empty.");

    public static readonly Error NameTooLong = Field("department.name-too-long", nameof(Department.Name), $"A department's name has at most {Department.NameMaxLength} characters.");

    public static readonly Error VersionInvalid = Field("department.version-invalid", "Version", "The version is not one the API issued for a department.");

    public static readonly Error NotFound = Error.NotFound("department.not-found", "No department has this id.");

    public static readonly Error ParentNotFound = Error.NotFound("department.parent-not-found", "No department has the id given as the parent.");

    public static readonly Error NameTaken = Error.Conflict("department.name-taken", "Another department under the same parent already has this name.");

    public static readonly Error WouldContainItself = Error.Failure("department.would-contain-itself", "A department cannot move under itself or under a department within it.");

    public static readonly Error InUse = Error.Conflict("department.in-use", "The department still has departments within it, or a person is in it today or from a later date.");

    private static Error Field(string code, string member, string message) =>
        Error.Validation(code, message, new Dictionary<string, string[]>(StringComparer.Ordinal) { [member] = [message] });
}
