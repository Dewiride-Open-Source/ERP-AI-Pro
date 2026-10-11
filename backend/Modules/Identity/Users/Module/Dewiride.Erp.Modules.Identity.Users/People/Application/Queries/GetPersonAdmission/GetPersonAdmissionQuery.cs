using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Queries;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.GetPersonAdmission;

internal sealed record GetPersonAdmissionQuery(Guid EntraObjectId) : IQuery<PersonAdmission?>;
