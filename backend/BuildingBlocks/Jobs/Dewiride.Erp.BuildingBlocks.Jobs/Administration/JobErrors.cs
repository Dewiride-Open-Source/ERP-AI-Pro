using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Administration;

public static class JobErrors
{
    public static readonly Error RunNotFound = Error.NotFound("job.run-not-found", "No job run has this id.");

    public static readonly Error RunNotRerunnable = Error.Conflict("job.run-not-rerunnable", "Only a run that failed, gave up or was cancelled can run again.");

    public static readonly Error RunFinished = Error.Conflict("job.run-finished", "The run has finished, so there is nothing to cancel.");

    public static readonly Error JobNotDefined = Error.Conflict("job.not-defined", "This release of the ERP does not define the job of this run, so the run cannot run again.");
}
