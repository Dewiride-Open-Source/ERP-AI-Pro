using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;

namespace Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Queries.ListJobs;

internal sealed record ListJobsQuery : IQuery<IReadOnlyList<JobDetails>>;
