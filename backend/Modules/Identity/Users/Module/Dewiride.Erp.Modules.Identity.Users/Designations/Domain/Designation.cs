using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Domain;

// The list of job titles a person's position chooses from. A designation is master data, so deleting one soft-deletes it, and
// the positions that named it keep showing its name.
internal sealed class Designation : AggregateRoot<DesignationId>, IAuditable, ISoftDeletable, IVersioned
{
    public const int NameMaxLength = 100;

    private Designation(DesignationId id)
        : base(id)
    {
    }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static Result<Designation> Create(string name)
    {
        var designation = new Designation(DesignationId.Create());
        var named = designation.Rename(name);

        return named.IsFailure ? named.Error! : designation;
    }

    public Result Rename(string name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return DesignationErrors.NameRequired;
        }

        if (trimmed.Length > NameMaxLength)
        {
            return DesignationErrors.NameTooLong;
        }

        Name = trimmed;

        return Result.Success();
    }
}
