using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.Departments.Domain;
using Dewiride.Erp.Modules.Identity.Users.Designations.Domain;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Domain;

// A person's position from one date until the date of their next: their department, their designation and the manager they
// report to, each of which may be empty from that date. A position is history, not master data, and is never edited: a
// correction withdraws it and records another.
internal sealed class Position : AggregateRoot<PositionId>, IAuditable
{
    private Position(PositionId id)
        : base(id)
    {
    }

    public UserId UserId { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    public DepartmentId? DepartmentId { get; private set; }

    public DesignationId? DesignationId { get; private set; }

    public UserId? ManagerId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public static Result<Position> Record(UserId userId, DateOnly effectiveFrom, DepartmentId? departmentId, DesignationId? designationId, UserId? managerId)
    {
        if (managerId == userId)
        {
            return PositionErrors.SelfManaged;
        }

        return new Position(PositionId.Create())
        {
            UserId = userId,
            EffectiveFrom = effectiveFrom,
            DepartmentId = departmentId,
            DesignationId = designationId,
            ManagerId = managerId,
        };
    }
}
