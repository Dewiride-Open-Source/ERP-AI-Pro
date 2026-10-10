using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.ListPeople;

internal sealed record ListPeopleQuery(ListRequest Request) : IQuery<PagedResult<PersonDetails>>;
