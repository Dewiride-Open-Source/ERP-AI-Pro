using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Commands.RerunJobRun;

internal sealed class RerunJobRunHandler(IJobAdministration administration) : ICommandHandler<RerunJobRunCommand, JobRunDetails>
{
    public Task<Result<JobRunDetails>> HandleAsync(RerunJobRunCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return administration.RerunAsync(command.Id, cancellationToken);
    }
}
