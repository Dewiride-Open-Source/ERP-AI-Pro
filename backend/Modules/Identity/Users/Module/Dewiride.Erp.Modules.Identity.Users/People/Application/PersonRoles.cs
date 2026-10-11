using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application;

internal static class PersonRoles
{
    public static IReadOnlyList<ErpRole> ToErpRoles(IEnumerable<PersonRole> roles) => [.. roles.Distinct().Select(ToErpRole)];

    public static PersonRole ToPersonRole(ErpRole role) => role switch
    {
        ErpRole.Finance => PersonRole.Finance,
        ErpRole.Hr => PersonRole.Hr,
        ErpRole.Manager => PersonRole.Manager,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Only Finance, HR and Manager are assigned in the ERP."),
    };

    private static ErpRole ToErpRole(PersonRole role) => role switch
    {
        PersonRole.Finance => ErpRole.Finance,
        PersonRole.Hr => ErpRole.Hr,
        PersonRole.Manager => ErpRole.Manager,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };
}
