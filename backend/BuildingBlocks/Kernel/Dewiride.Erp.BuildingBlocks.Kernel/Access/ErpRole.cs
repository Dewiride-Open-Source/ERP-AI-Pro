namespace Dewiride.Erp.BuildingBlocks.Kernel.Access;

// The built-in roles of the ERP: a person holds Administrator through the Entra app role Erp.Admin and Employee through
// Erp.User, and an administrator assigns them the others in the ERP. Members are stored and sent by name, so renaming one
// changes the data and the contract.
public enum ErpRole
{
    Employee,
    Finance,
    Hr,
    Manager,
    Administrator,
}
