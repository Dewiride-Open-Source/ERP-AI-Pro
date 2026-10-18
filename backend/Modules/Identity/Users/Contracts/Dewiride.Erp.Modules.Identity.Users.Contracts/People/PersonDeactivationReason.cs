namespace Dewiride.Erp.Modules.Identity.Users.Contracts.People;

// Why a person's record was deactivated: an administrator did it, or the check of the company directory found the person's
// Entra account disabled or deleted.
public enum PersonDeactivationReason
{
    Administrator = 1,
    AccountDisabled = 2,
    AccountDeleted = 3,
}
