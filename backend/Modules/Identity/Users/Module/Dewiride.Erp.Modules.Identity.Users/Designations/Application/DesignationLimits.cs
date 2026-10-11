using Dewiride.Erp.Modules.Identity.Users.Designations.Domain;
using Dewiride.Erp.Modules.Identity.Users.Shared.Versioning;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Application;

internal static class DesignationLimits
{
    public const int NameMaxLength = Designation.NameMaxLength;

    public const int VersionMaxLength = RecordVersion.InputMaxLength;
}
