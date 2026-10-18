using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application;

internal static class DeactivationReasons
{
    public static PersonDeactivationReason ToPersonDeactivationReason(DeactivationReason reason) => reason switch
    {
        DeactivationReason.Administrator => PersonDeactivationReason.Administrator,
        DeactivationReason.AccountDisabled => PersonDeactivationReason.AccountDisabled,
        DeactivationReason.AccountDeleted => PersonDeactivationReason.AccountDeleted,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
    };
}
