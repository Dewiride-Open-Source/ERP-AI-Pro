using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Application;

// A person's positions with the names of what they name. History names what was there then, so soft-deleted departments,
// designations and managers keep their names; ignoring the soft-delete filter covers the whole query.
internal static class PositionsWithNames
{
    public static IQueryable<PositionRow> Of(UsersDbContext context, UserId person)
    {
        ArgumentNullException.ThrowIfNull(context);

        return
            from position in context.Positions.IncludeDeleted()
            where position.UserId == person
            from department in context.Departments.Where(d => d.Id == position.DepartmentId).DefaultIfEmpty()
            from designation in context.Designations.Where(d => d.Id == position.DesignationId).DefaultIfEmpty()
            from manager in context.Users.Where(m => m.Id == position.ManagerId).DefaultIfEmpty()
            select new PositionRow
            {
                Position = position,
                DepartmentName = department!.Name,
                DesignationName = designation!.Name,
                ManagerDisplayName = manager!.DisplayName,
            };
    }
}
