using Dewiride.Erp.Modules.Identity.Users.Designations.Domain;
using Dewiride.Erp.Modules.Identity.Users.Shared.Versioning;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Application;

internal sealed record DesignationDetails(Guid Id, string Name, string Version)
{
    public static DesignationDetails From(Designation designation)
    {
        ArgumentNullException.ThrowIfNull(designation);

        return new(designation.Id.Value, designation.Name, RecordVersion.Encode(designation.RowVersion));
    }
}
