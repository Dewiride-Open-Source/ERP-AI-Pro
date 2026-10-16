using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;

namespace Dewiride.Erp.Modules.Platform.Audit.Changes.Application.Queries.ListChanges;

internal sealed record ListChangesQuery(ListRequest Request) : IQuery<PagedResult<ChangeRecordDetails>>;
