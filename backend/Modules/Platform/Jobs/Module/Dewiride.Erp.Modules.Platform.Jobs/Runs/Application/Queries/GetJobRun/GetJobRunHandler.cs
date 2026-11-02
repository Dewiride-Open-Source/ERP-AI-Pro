using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Queries.GetJobRun;

internal sealed class GetJobRunHandler(IJobAdministration administration) : IQueryHandler<GetJobRunQuery, JobRunHistory>
{
    public Task<Result<JobRunHistory>> HandleAsync(GetJobRunQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return administration.GetRunAsync(query.Id, cancellationToken);
    }
}
