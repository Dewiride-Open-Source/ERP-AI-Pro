using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;
using Dewiride.Erp.Modules.Identity.Users.Designations.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Application;

internal static class DesignationLimits
{
    public const int NameMaxLength = Designation.NameMaxLength;

    public const int VersionMaxLength = RecordVersion.InputMaxLength;
}
