using Dewiride.Erp.BuildingBlocks.Application.Queries;

namespace Dewiride.Erp.Modules.Identity.Users.Designations.Application.Queries.GetDesignation;

internal sealed record GetDesignationQuery(Guid Id) : IQuery<DesignationDetails>;
