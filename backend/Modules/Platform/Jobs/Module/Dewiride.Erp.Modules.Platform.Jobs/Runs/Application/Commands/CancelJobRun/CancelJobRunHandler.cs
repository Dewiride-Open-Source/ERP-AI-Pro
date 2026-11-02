using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Commands.CancelJobRun;

internal sealed class CancelJobRunHandler(IJobAdministration administration) : ICommandHandler<CancelJobRunCommand, JobRunDetails>
{
    public Task<Result<JobRunDetails>> HandleAsync(CancelJobRunCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return administration.CancelAsync(command.Id, cancellationToken);
    }
}
