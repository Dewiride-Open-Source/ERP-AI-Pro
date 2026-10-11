using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Domain;

internal static class DesignationErrors
{
    public static readonly Error NameRequired = Field("designation.name-required", nameof(Designation.Name), "A designation's name must not be empty.");

    public static readonly Error NameTooLong = Field("designation.name-too-long", nameof(Designation.Name), $"A designation's name has at most {Designation.NameMaxLength} characters.");

    public static readonly Error VersionInvalid = Field("designation.version-invalid", "Version", "The version is not one the API issued for a designation.");

    public static readonly Error NotFound = Error.NotFound("designation.not-found", "No designation has this id.");

    public static readonly Error NameTaken = Error.Conflict("designation.name-taken", "Another designation already has this name.");

    public static readonly Error InUse = Error.Conflict("designation.in-use", "A person holds the designation today or from a later date.");

    private static Error Field(string code, string member, string message) =>
        Error.Validation(code, message, new Dictionary<string, string[]>(StringComparer.Ordinal) { [member] = [message] });
}
