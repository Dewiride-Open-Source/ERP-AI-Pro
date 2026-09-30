namespace Dewiride.Erp.Testing.Authentication;

public static class TestUsers
{
    public const string UserRole = "Erp.User";

    public const string AdminRole = "Erp.Admin";

    public static TestUser Accountant { get; } = new(Guid.Parse("3f0c2a8e-6b1d-4c7e-9a52-0d8e4f1b7c31"), "Asha Rao", "asha.rao@example.com", [UserRole]);

    public static TestUser Administrator { get; } = new(Guid.Parse("8b6e1f47-2c9a-4d3b-b5e0-7a19c4d2e865"), "Vikram Iyer", "vikram.iyer@example.com", [UserRole, AdminRole]);

    public static IReadOnlyList<TestUser> All { get; } = [Accountant, Administrator];

    public static TestUser? Find(Guid objectId) => All.FirstOrDefault(user => user.ObjectId == objectId);
}
