using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Queries;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.FindRegisteredPeople;

internal sealed record FindRegisteredPeopleQuery(IReadOnlyList<DirectoryPerson> People) : IQuery<IReadOnlyDictionary<Guid, Guid>>;
