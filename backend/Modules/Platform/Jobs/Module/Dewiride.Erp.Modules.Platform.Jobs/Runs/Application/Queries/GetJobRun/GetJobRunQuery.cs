using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Queries.GetJobRun;

internal sealed record GetJobRunQuery(JobRunId Id) : IQuery<JobRunHistory>;
