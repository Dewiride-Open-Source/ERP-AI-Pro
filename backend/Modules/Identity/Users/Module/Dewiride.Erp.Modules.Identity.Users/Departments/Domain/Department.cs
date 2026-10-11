using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Identity.Users.Departments.Domain;

// A department sits under at most one other, so the departments form a tree, and it holds the people whose position names it.
// The tree is not dated: a move or a new name holds for every date, past ones included. Departments are master data, so
// deleting one soft-deletes it and the positions that named it keep showing its name.
internal sealed class Department : AggregateRoot<DepartmentId>, IAuditable, ISoftDeletable, IVersioned
{
    public const int NameMaxLength = 100;

    private Department(DepartmentId id)
        : base(id)
    {
    }

    public string Name { get; private set; } = string.Empty;

    public DepartmentId? ParentId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static Result<Department> Create(string name, DepartmentId? parentId)
    {
        var department = new Department(DepartmentId.Create()) { ParentId = parentId };
        var named = department.Rename(name);

        return named.IsFailure ? named.Error! : department;
    }

    public Result Rename(string name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return DepartmentErrors.NameRequired;
        }

        if (trimmed.Length > NameMaxLength)
        {
            return DepartmentErrors.NameTooLong;
        }

        Name = trimmed;

        return Result.Success();
    }

    public Result MoveUnder(DepartmentId? parentId, DepartmentTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        if (parentId is { } parent && tree.IsWithin(parent, Id))
        {
            return DepartmentErrors.WouldContainItself;
        }

        ParentId = parentId;

        return Result.Success();
    }
}
