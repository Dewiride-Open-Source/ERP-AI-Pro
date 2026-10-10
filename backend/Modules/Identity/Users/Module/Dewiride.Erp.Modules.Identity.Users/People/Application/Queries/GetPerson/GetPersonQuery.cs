using Dewiride.Erp.BuildingBlocks.Application.Queries;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.GetPerson;

internal sealed record GetPersonQuery(Guid Id) : IQuery<PersonDetails>;
