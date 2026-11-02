using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;

namespace Dewiride.Erp.Modules.Platform.Jobs.Runs.Application.Queries.ListJobRuns;

internal sealed record ListJobRunsQuery(ListRequest Request) : IQuery<PagedResult<JobRunDetails>>;
