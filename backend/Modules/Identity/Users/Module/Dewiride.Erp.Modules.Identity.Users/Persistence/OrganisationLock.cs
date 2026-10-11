using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;

namespace Dewiride.Erp.Modules.Identity.Users.Persistence;

// The rules of these writes read what the others change. A reporting cycle, or a department moved under one of its own, needs
// two changes that each pass alone; a department, designation or manager can be deleted while a position naming it is saved.
// So every write to the department tree or the positions, and every delete of something a position names, takes this lock
// first, and their checks run one after the other.
internal static class OrganisationLock
{
    public const string Resource = "identity_users.organisation";

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static readonly Error Busy = Error.Conflict(EfUnitOfWork.ConcurrencyConflictCode, "Another change to the departments, designations or positions is being saved; try again.");

    public static Task<bool> TryAcquireAsync(UsersDbContext context, CancellationToken cancellationToken) =>
        TransactionLocks.TryAcquireExclusiveAsync(context, Resource, Timeout, cancellationToken);
}
