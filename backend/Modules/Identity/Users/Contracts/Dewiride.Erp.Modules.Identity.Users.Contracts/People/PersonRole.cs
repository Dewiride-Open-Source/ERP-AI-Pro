namespace Dewiride.Erp.Modules.Identity.Users.Contracts.People;

// The roles an administrator assigns in the ERP; a person holds Administrator and Employee through the app roles of their
// Entra account, which the ERP never assigns.
public enum PersonRole
{
    Finance,
    Hr,
    Manager,
}
