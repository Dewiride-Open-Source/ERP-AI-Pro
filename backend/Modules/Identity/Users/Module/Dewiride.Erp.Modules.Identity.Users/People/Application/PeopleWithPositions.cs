using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Dewiride.Erp.Modules.Identity.Users.Persistence;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application;

// Every person whose record is not deleted, with the position they hold on the date: their latest position that starts on or
// before it. Soft-deleted departments, designations and managers keep their names here, because a position from before their
// deletion still names them; ignoring the soft-delete filter covers the whole query, so the people are filtered by hand.
internal static class PeopleWithPositions
{
    public static IQueryable<PersonRow> On(UsersDbContext context, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(context);

        return
            from user in context.Users.IncludeDeleted()
            where !user.IsDeleted
            from position in context.Positions
                .Where(p => p.UserId == user.Id && p.EffectiveFrom <= date)
                .OrderByDescending(p => p.EffectiveFrom)
                .Take(1)
                .DefaultIfEmpty()
            from department in context.Departments.Where(d => d.Id == position!.DepartmentId).DefaultIfEmpty()
            from designation in context.Designations.Where(d => d.Id == position!.DesignationId).DefaultIfEmpty()
            from manager in context.Users.Where(m => m.Id == position!.ManagerId).DefaultIfEmpty()
            select new PersonRow
            {
                User = user,
                DepartmentId = position!.DepartmentId,
                DepartmentName = department!.Name,
                DesignationId = position.DesignationId,
                DesignationName = designation!.Name,
                ManagerId = position.ManagerId,
                ManagerDisplayName = manager!.DisplayName,
            };
    }
}
