using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Domain;

internal static class PositionErrors
{
    public static readonly Error NotFound = Error.NotFound("position.not-found", "The person has no position with this id.");

    public static readonly Error DateTaken = Error.Conflict("position.date-taken", "The person already has a position from this date; withdraw it first.");

    public static readonly Error DepartmentNotFound = Error.NotFound("position.department-not-found", "No department has the id given for the position.");

    public static readonly Error DesignationNotFound = Error.NotFound("position.designation-not-found", "No designation has the id given for the position.");

    public static readonly Error ManagerNotFound = Error.NotFound("position.manager-not-found", "No person has the id given as the manager.");

    public static readonly Error ManagerInactive = Error.Failure("position.manager-inactive", "The manager's record is deactivated, so they cannot be anyone's manager today or later.");

    public static readonly Error SelfManaged = Error.Failure("position.self-managed", "A person cannot be their own manager.");

    public static readonly Error ReportingCycle = Error.Failure("position.reporting-cycle", "The person would report, through their managers, to themself.");

    public static readonly Error PreviousNoLongerValid = Error.Failure("position.previous-no-longer-valid", "Withdrawing this position makes the one before it hold again, and that one names a department, designation or manager deleted since, or a manager no longer active; record a new position instead.");
}
