using Dewiride.Erp.BuildingBlocks.Application.Queries;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.IsPersonAdmitted;

internal sealed record IsPersonAdmittedQuery(Guid EntraObjectId) : IQuery<bool>;
