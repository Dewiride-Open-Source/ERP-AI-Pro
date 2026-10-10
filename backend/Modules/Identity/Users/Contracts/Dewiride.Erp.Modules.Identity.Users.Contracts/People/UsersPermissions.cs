namespace Dewiride.Erp.Modules.Identity.Users.Contracts.People;

public static class UsersPermissions
{
    public const string Read = "identity.users.people.read";

    public const string Register = "identity.users.people.register";

    public const string Update = "identity.users.people.update";

    public const string ChangeStatus = "identity.users.people.change-status";

    public const string Delete = "identity.users.people.delete";

    public static IReadOnlyList<string> All { get; } = [Read, Register, Update, ChangeStatus, Delete];
}
